using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UltraPinball.Core.Game;

namespace UltraPinball.MediaBridge;

/// <summary>
/// TCP client that connects to a media controller server and forwards game events
/// as newline-delimited JSON. Implements <see cref="IMediaEventSink"/> so it can
/// be assigned to <see cref="GameController.Media"/>.
///
/// <para>
/// The media controller is the server; the game engine is the client. On connect,
/// the client sends a handshake containing the game name, version, scene manifest,
/// and a CRC of the scene list. The server validates the CRC and responds with
/// <c>handshake_ok</c> or <c>handshake_failed</c>.
/// </para>
/// <para>
/// After a successful handshake, all <see cref="Post"/> calls are serialized to
/// JSON and sent as a single line. If the connection drops, <see cref="Post"/>
/// silently drops events so the game continues without media.
/// </para>
/// </summary>
public sealed class MediaBridgeClient : IMediaEventSink, IDisposable
{
    private TcpClient? _tcp;
    private StreamWriter? _writer;
    private bool _connected;
    private readonly ILogger _log;

    /// <summary>Media controller hostname. Default: <c>127.0.0.1</c>.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Media controller TCP port. Default: <c>9000</c>.</summary>
    public int Port { get; init; } = 9000;

    /// <summary>Total time to spend retrying the initial connection. Default: 5 seconds.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public MediaBridgeClient(ILogger<MediaBridgeClient>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    // ── Connection ────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to the media controller, sends the handshake, and waits for
    /// acknowledgement. Retries the TCP connection until <see cref="ConnectTimeout"/>
    /// elapses, to give a subprocess MC time to start listening.
    /// </summary>
    /// <param name="gameName">Human-readable game name sent in the handshake.</param>
    /// <param name="gameVersion">Game version string, e.g. <c>"1.0.0"</c>.</param>
    /// <param name="mediaVersion">
    /// Media asset version string. The MC validates this against its own expected version.
    /// </param>
    /// <param name="scenes">
    /// Complete list of every event type this game will ever send. The MC checks
    /// it has a handler for each, and compares a CRC of the sorted list to verify sync.
    /// </param>
    /// <returns><c>true</c> if the handshake succeeded; <c>false</c> if the MC is
    /// unavailable or rejected the handshake. The game continues either way.</returns>
    public async Task<bool> ConnectAsync(
        string gameName,
        string gameVersion,
        string mediaVersion,
        string[] scenes,
        CancellationToken ct = default)
    {
        // Retry TCP connect until timeout — gives a freshly launched MC time to start.
        var deadline = DateTime.UtcNow + ConnectTimeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var attempt = new TcpClient();
            try
            {
                await attempt.ConnectAsync(Host, Port, ct).ConfigureAwait(false);
                _tcp = attempt;
                break;
            }
            catch (SocketException)
            {
                attempt.Dispose();
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }

        if (_tcp == null)
        {
            _log.LogWarning("MediaBridge: could not connect to {Host}:{Port} — running without media.", Host, Port);
            return false;
        }

        _log.LogDebug("MediaBridge: TCP connected to {Host}:{Port}.", Host, Port);

        var stream = _tcp.GetStream();
        _writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        // ── Handshake ─────────────────────────────────────────────────────────

        var handshake = new
        {
            type       = "handshake",
            game       = gameName,
            game_version  = gameVersion,
            media_version = mediaVersion,
            scenes_crc = ComputeScenesCrc(scenes),
            scenes,
        };
        await _writer.WriteLineAsync(JsonSerializer.Serialize(handshake)).ConfigureAwait(false);

        // Read one response line with a 5-second timeout.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        string? responseLine;
        try
        {
            // StreamReader with leaveOpen so closing it doesn't close the stream.
            using var reader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false, bufferSize: 512, leaveOpen: true);
            responseLine = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("MediaBridge: handshake timed out.");
            return false;
        }

        if (responseLine == null)
        {
            _log.LogWarning("MediaBridge: MC closed connection during handshake.");
            return false;
        }

        var response = JsonNode.Parse(responseLine);
        if (response?["type"]?.GetValue<string>() != "handshake_ok")
        {
            var reason = response?["reason"]?.GetValue<string>() ?? "unknown";
            _log.LogError("MediaBridge: handshake rejected — {Reason}.", reason);
            return false;
        }

        _log.LogInformation("MediaBridge: handshake OK. Media controller connected.");
        _connected = true;
        return true;
    }

    // ── IMediaEventSink ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Post(string eventType, object? data = null)
    {
        if (!_connected || _writer == null) return;

        try
        {
            var node = new JsonObject
            {
                ["type"] = eventType,
                ["ts"]   = DateTime.UtcNow.ToString("O"),
            };

            if (data != null)
                node["data"] = JsonSerializer.SerializeToNode(data, data.GetType());

            _writer.WriteLine(node.ToJsonString());
        }
        catch (Exception ex)
        {
            _log.LogWarning("MediaBridge: send failed ({Error}). Media events suppressed.", ex.Message);
            _connected = false;
        }
    }

    // ── CRC ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes a short CRC from a list of scene names: SHA-256 of the sorted,
    /// newline-joined list, returned as the first 8 lowercase hex characters.
    /// The media controller uses the same algorithm to verify sync.
    /// </summary>
    public static string ComputeScenesCrc(IEnumerable<string> scenes)
    {
        var manifest = string.Join('\n', scenes.OrderBy(s => s));
        var bytes    = SHA256.HashData(Encoding.UTF8.GetBytes(manifest));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _connected = false;
        _writer?.Dispose();
        _tcp?.Dispose();
    }
}
