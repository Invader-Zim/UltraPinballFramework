using System.Collections.Concurrent;
using UltraPinball.Core.Devices;
using UltraPinball.Core.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Core.Game;

/// <summary>
/// The central game object. Owns the hardware platform, device collections,
/// mode queue, player list, and game loop.
///
/// Subclass this to add machine-specific game flow, or use it directly with
/// modes for a purely composition-based approach.
/// </summary>
public class GameController
{
    // ── Public surface for modes ──────────────────────────────────────────────

    public DeviceCollection<Switch> Switches => _config.Switches;
    public DeviceCollection<Coil> Coils => _config.Coils;
    public DeviceCollection<Led> Leds => _config.Leds;
    public ModeQueue Modes { get; }
    public IHardwarePlatform Hardware { get; }

    /// <summary>
    /// Optional media event sink. Set this before <see cref="RunAsync"/> to enable
    /// media output. When <c>null</c> all <c>Media?.Post()</c> calls are no-ops.
    /// </summary>
    public IMediaEventSink? Media { get; set; }

    // ── Game state ────────────────────────────────────────────────────────────

    public IReadOnlyList<Player> Players => _players;
    public Player? CurrentPlayer => _players.Count > 0 ? _players[_currentPlayerIndex] : null;
    public int Ball { get; private set; }
    public int BallsPerGame { get; set; } = 3;
    public int MaxPlayers { get; set; } = 4;
    public bool IsGameInProgress => Ball > 0;

    // ── Game lifecycle events ─────────────────────────────────────────────────

    public event Action? GameStarted;
    public event Action<int>? BallStarting;    // ball number
    public event Action<int>? BallEnded;       // ball number
    public event Action? GameEnded;
    public event Action<Player>? PlayerAdded;

    // ── Internals ─────────────────────────────────────────────────────────────

    private readonly MachineConfig _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GameController> _log;
    private readonly List<Player> _players = new();
    private int _currentPlayerIndex;
    private DateTime _ballStartTime;
    private readonly ConcurrentQueue<(int HwNumber, SwitchState State)> _pendingSwitchEvents = new();
    private readonly List<(Mode Mode, ModeLifecycle Lifecycle)> _registeredModes = new();

    /// <summary>Initialises the game controller with a machine definition and hardware platform.</summary>
    /// <param name="config">The machine definition that declares all switches, coils, and LEDs.</param>
    /// <param name="platform">
    /// The hardware abstraction to use. Pass a <c>SimulatorPlatform</c>
    /// for keyboard-driven development and testing, or a real platform implementation for hardware.
    /// </param>
    /// <param name="loggerFactory">
    /// Optional logger factory. Defaults to <see cref="Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory"/>
    /// (no logging) if not supplied.
    /// </param>
    public GameController(MachineConfig config, IHardwarePlatform platform,
                          ILoggerFactory? loggerFactory = null)
    {
        _config = config;
        Hardware = platform;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _log = _loggerFactory.CreateLogger<GameController>();
        Modes = new ModeQueue(this, _loggerFactory.CreateLogger<ModeQueue>());
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to hardware, syncs initial switch states, and starts the game loop.
    /// Blocks until <paramref name="ct"/> is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        _log.LogInformation("Connecting to hardware...");
        await Hardware.ConnectAsync(ct);
        _config.Initialize(Hardware);

        // Sync initial switch states from hardware
        var initialStates = await Hardware.GetInitialSwitchStatesAsync();
        foreach (var (hwNum, state) in initialStates)
        {
            if (_config.Switches.TryGetByHw(hwNum, out var sw) && sw != null)
            {
                sw.State = state;
                sw.LastChangedAt = DateTime.UtcNow;
            }
        }

        // Wire up hardware switch events (raised on background thread)
        Hardware.SwitchChanged += (hwNum, state) => _pendingSwitchEvents.Enqueue((hwNum, state));

        _log.LogInformation("Hardware ready. Starting game loop.");
        OnStartup();

        // ── Game loop ──────────────────────────────────────────────────────────
        var lastTick = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var delta = (float)(now - lastTick).TotalSeconds;
            lastTick = now;

            // Drain switch event queue (enqueued from hardware background thread)
            while (_pendingSwitchEvents.TryDequeue(out var evt))
                ProcessSwitchEvent(evt.HwNumber, evt.State);

            Modes.Tick(delta);

            // ~1ms sleep keeps CPU usage reasonable while maintaining responsiveness
            await Task.Delay(1, ct).ConfigureAwait(false);
        }

        await Hardware.DisconnectAsync();
        _log.LogInformation("Game loop stopped.");
    }

    /// <summary>
    /// Called once after hardware is ready, before the game loop starts.
    /// Override to call <see cref="RegisterMode(Mode)"/> for all modes used by the game.
    /// </summary>
    protected virtual void OnStartup() { }

    /// <summary>
    /// Registers a mode with a lifecycle that controls when it is automatically
    /// added to and removed from the <see cref="ModeQueue"/>.
    /// <list type="bullet">
    ///   <item><see cref="ModeLifecycle.System"/> — added immediately, never removed.</item>
    ///   <item><see cref="ModeLifecycle.Game"/> — added on <see cref="StartGame"/>, removed on <see cref="EndGame"/>.</item>
    ///   <item><see cref="ModeLifecycle.Ball"/> — added on <see cref="StartBall"/>, removed on <see cref="EndBall"/>.</item>
    ///   <item><see cref="ModeLifecycle.Manual"/> — not managed; caller uses <see cref="ModeQueue"/> directly.</item>
    /// </list>
    /// Call this from <see cref="OnStartup"/>. Manual-lifecycle modes may also be passed here
    /// as a no-op registration for book-keeping, or simply managed via <see cref="Modes"/> directly.
    /// </summary>
    /// <summary>
    /// Registers a mode using its <see cref="Mode.DefaultLifecycle"/>.
    /// Prefer this overload — it keeps lifecycle knowledge with the mode itself.
    /// </summary>
    public void RegisterMode(Mode mode) => RegisterMode(mode, mode.DefaultLifecycle);

    /// <summary>Registers a mode with an explicit lifecycle, overriding the mode's default.</summary>
    public void RegisterMode(Mode mode, ModeLifecycle lifecycle)
    {
        _registeredModes.Add((mode, lifecycle));
        if (lifecycle == ModeLifecycle.System)
            Modes.Add(mode);
    }

    // ── Game flow (call from modes or override) ───────────────────────────────

    /// <summary>
    /// Adds a new player to the current game and raises <see cref="PlayerAdded"/>.
    /// Override to enforce a maximum player count or apply machine-specific player setup.
    /// </summary>
    /// <returns>The newly created player.</returns>
    public virtual Player AddPlayer()
    {
        var player = CreatePlayer($"Player {_players.Count + 1}");
        _players.Add(player);
        _log.LogInformation("Player added: {Player}", player.Name);
        PlayerAdded?.Invoke(player);
        return player;
    }

    /// <summary>
    /// Starts a new game: clears players, adds one player, sets ball to 1, and begins the first ball.
    /// Does nothing if a game is already in progress.
    /// </summary>
    public virtual void StartGame()
    {
        if (IsGameInProgress) return;
        _players.Clear();
        _currentPlayerIndex = 0;
        Ball = 1;
        AddPlayer();
        _log.LogInformation("Game started.");
        Media?.Post("game_started", new { player = CurrentPlayer?.Name, balls_per_game = BallsPerGame });
        GameStarted?.Invoke();
        foreach (var (mode, lc) in _registeredModes)
            if (lc == ModeLifecycle.Game)
                Modes.Add(mode);
        StartBall();
    }

    public virtual void StartBall()
    {
        foreach (var (mode, lc) in _registeredModes)
            if (lc == ModeLifecycle.Ball && !Modes.Contains(mode))
                Modes.Add(mode);
        _ballStartTime = DateTime.UtcNow;
        _log.LogInformation("Ball {Ball} starting for {Player}.", Ball, CurrentPlayer?.Name);
        Media?.Post("ball_starting", new { ball = Ball, player = CurrentPlayer?.Name });
        BallStarting?.Invoke(Ball);
    }

    /// <summary>
    /// Ends the current ball. Removes Ball-lifecycle modes, handles extra balls,
    /// player rotation, and ball increment. Calls <see cref="EndGame"/> when the
    /// final ball of the final player is done.
    /// </summary>
    public virtual void EndBall()
    {
        if (CurrentPlayer != null)
            CurrentPlayer.GameTime += DateTime.UtcNow - _ballStartTime;

        _log.LogInformation("Ball {Ball} ended. Score: {Score}", Ball, CurrentPlayer?.Score);
        Media?.Post("ball_ended", new { ball = Ball, player = CurrentPlayer?.Name, score = CurrentPlayer?.Score });
        BallEnded?.Invoke(Ball);

        foreach (var (mode, lc) in _registeredModes)
            if (lc == ModeLifecycle.Ball)
                Modes.Remove(mode);

        if (CurrentPlayer!.ExtraBalls > 0)
        {
            CurrentPlayer.ExtraBalls--;
            StartBall();
            return;
        }

        if (_currentPlayerIndex + 1 < _players.Count)
        {
            _currentPlayerIndex++;
        }
        else
        {
            Ball++;
            _currentPlayerIndex = 0;
        }

        if (Ball > BallsPerGame)
            EndGame();
        else
            StartBall();
    }

    public virtual void EndGame()
    {
        foreach (var (mode, lc) in _registeredModes)
            if (lc == ModeLifecycle.Game)
                Modes.Remove(mode);
        Media?.Post("game_ended", new { scores = Players.Select(p => new { name = p.Name, score = p.Score }).ToArray() });
        _log.LogInformation("Game ended.");
        Ball = 0;
        GameEnded?.Invoke();
    }

    // ── Helpers for modes ─────────────────────────────────────────────────────

    /// <summary>Override to use a custom Player subclass.</summary>
    protected virtual Player CreatePlayer(string name) => new Player(name);

    /// <summary>Creates a logger for a mode or other component by name.</summary>
    public ILogger CreateLogger(string name) => _loggerFactory.CreateLogger(name);

    // ── Switch event processing ───────────────────────────────────────────────

    private void ProcessSwitchEvent(int hwNumber, SwitchState newState)
    {
        if (!_config.Switches.TryGetByHw(hwNumber, out var sw) || sw == null)
        {
            _log.LogWarning("Switch event for unregistered hw number 0x{Hw:X2}. Ignored.", hwNumber);
            return;
        }

        if (sw.State == newState) return; // deduplicate

        sw.State = newState;
        sw.LastChangedAt = DateTime.UtcNow;

        _log.LogDebug("Switch {Name}: {State} (active={Active})", sw.Name, newState, sw.IsActive);
        Modes.HandleSwitchEvent(sw, newState);
    }
}
