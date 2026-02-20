using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// ── Start listening ───────────────────────────────────────────────────────────

var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 9000;

Console.WriteLine($"UltraPinball MediaController — listening on port {port}");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

var listener = new TcpListener(IPAddress.Any, port);
listener.Start();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    while (!cts.IsCancellationRequested)
    {
        var client = await listener.AcceptTcpClientAsync(cts.Token);
        _ = HandleClientAsync(client, cts.Token);
    }
}
catch (OperationCanceledException) { }
finally
{
    listener.Stop();
    Console.WriteLine();
    Console.WriteLine("MediaController stopped.");
}

// ── Per-connection handler ────────────────────────────────────────────────────

static async Task HandleClientAsync(TcpClient client, CancellationToken ct)
{
    var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
    Console.WriteLine($"[{Now()}] Client connected: {remote}");

    using (client)
    {
        var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        try
        {
            // ── Handshake ─────────────────────────────────────────────────────
            var handshakeLine = await reader.ReadLineAsync(ct);
            if (handshakeLine == null)
            {
                Console.WriteLine($"[{Now()}] {remote}: connection closed before handshake.");
                return;
            }

            var handshake = JsonNode.Parse(handshakeLine);
            if (handshake?["type"]?.GetValue<string>() != "handshake")
            {
                Console.WriteLine($"[{Now()}] {remote}: unexpected message (expected handshake): {handshakeLine}");
                return;
            }

            var game    = handshake["game"]?.GetValue<string>()         ?? "?";
            var version = handshake["game_version"]?.GetValue<string>() ?? "?";
            Console.WriteLine($"[{Now()}] Handshake  game={game}  version={version}");

            await writer.WriteLineAsync(JsonSerializer.Serialize(new { type = "handshake_ok" }));

            // ── Event loop ────────────────────────────────────────────────────
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                var msg  = JsonNode.Parse(line);
                var type = msg?["type"]?.GetValue<string>() ?? "unknown";
                var ts   = msg?["ts"]?.GetValue<string>()   ?? Now();
                var data = msg?["data"];

                Console.Write($"[{ts}]  {type}");
                if (data != null)
                    Console.Write($"  {data.ToJsonString()}");
                Console.WriteLine();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[{Now()}] {remote}: {ex.Message}");
        }
    }

    Console.WriteLine($"[{Now()}] Client disconnected: {remote}");
}

static string Now() => DateTime.Now.ToString("HH:mm:ss.fff");
