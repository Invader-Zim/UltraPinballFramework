using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;

// ── Logging ───────────────────────────────────────────────────────────────────

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

// ── Machine definition ────────────────────────────────────────────────────────
// Replace this with your actual machine config class.

var machine = new SampleMachine();

// ── Platform selection ────────────────────────────────────────────────────────
// Swap SimulatorPlatform for FastNeuronPlatform when running on real hardware.

var simulator = new SimulatorPlatform()
    .MapKey(ConsoleKey.Z, switchHwNumber: 0x00)  // Left flipper
    .MapKey(ConsoleKey.X, switchHwNumber: 0x01)  // Right flipper
    .MapKey(ConsoleKey.S, switchHwNumber: 0x10)  // Start button
    .MapKey(ConsoleKey.D, switchHwNumber: 0x20); // Drain

// ── Game ──────────────────────────────────────────────────────────────────────

var game = new SampleGame(machine, simulator, loggerFactory);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await game.RunAsync(cts.Token);

// ── Sample machine definition ─────────────────────────────────────────────────

class SampleMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("LeftFlipper",  hwNumber: 0x00, debounce: false);
        AddSwitch("RightFlipper", hwNumber: 0x01, debounce: false);
        AddSwitch("StartButton",  hwNumber: 0x10);
        AddSwitch("Drain",        hwNumber: 0x20);

        AddCoil("LeftFlipperMain",  hwNumber: 0x00, defaultPulseMs: 10);
        AddCoil("RightFlipperMain", hwNumber: 0x01, defaultPulseMs: 10);

        // Hardware-level flipper rules: the board fires the coil — no host round-trip
        AddFlipperRule("LeftFlipper",  mainCoil: "LeftFlipperMain");
        AddFlipperRule("RightFlipper", mainCoil: "RightFlipperMain");
    }
}

// ── Sample game ───────────────────────────────────────────────────────────────

class SampleGame : GameController
{
    public SampleGame(MachineConfig config, SimulatorPlatform platform,
                      ILoggerFactory loggerFactory)
        : base(config, platform, loggerFactory) { }

    protected override void OnStartup()
    {
        Modes.Add(new AttractMode());
        Modes.Add(new DrainMode());
    }
}

// ── Sample modes ──────────────────────────────────────────────────────────────

class AttractMode : Mode
{
    public AttractMode() : base(priority: 1) { }

    public override void ModeStarted()
    {
        Console.WriteLine("[ATTRACT] Waiting for start button (press S)...");
        AddSwitchHandler("StartButton", SwitchActivation.Active, OnStartPressed);
    }

    private SwitchHandlerResult OnStartPressed(UltraPinball.Core.Devices.Switch sw)
    {
        Console.WriteLine("[ATTRACT] Start pressed — starting game!");
        Game.StartGame();
        return SwitchHandlerResult.Stop;
    }
}

class DrainMode : Mode
{
    public DrainMode() : base(priority: 10) { }

    public override void ModeStarted()
    {
        AddSwitchHandler("Drain", SwitchActivation.Active, OnDrain);
    }

    private SwitchHandlerResult OnDrain(UltraPinball.Core.Devices.Switch sw)
    {
        if (!Game.IsGameInProgress) return SwitchHandlerResult.Continue;
        Console.WriteLine($"[DRAIN] Ball {Game.Ball} drained!  Score: {Game.CurrentPlayer?.Score:N0}");
        Game.EndBall();
        return SwitchHandlerResult.Stop;
    }
}
