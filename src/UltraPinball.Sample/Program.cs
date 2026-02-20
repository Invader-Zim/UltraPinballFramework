using Microsoft.Extensions.Logging;
using UltraPinball.Core.Devices;
using UltraPinball.MediaBridge;
using UltraPinball.Sample;
using UltraPinball.Simulator;

// ── Logging ───────────────────────────────────────────────────────────────────

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

// ── Machine ───────────────────────────────────────────────────────────────────

var machine = new SampleMachine();

// ── Platform ──────────────────────────────────────────────────────────────────
// Swap SimulatorPlatform for FastNeuronPlatform when running on real hardware.
//
// Keyboard layout (US):
//   S          = Start
//   Z / X      = Left / Right flipper
//   A / '      = Left / Right inlane
//   Q / P      = Left / Right outlane
//   D          = ShooterLane (simulate ball arriving / leaving)
//   1–5        = Trough0–4  (simulate a drain)

var platform = new SimulatorPlatform()
    // Cabinet
    .MapKey(ConsoleKey.S,    switchHwNumber: 0x0B, label: "Start")
    // Flippers
    .MapKey(ConsoleKey.Z,    switchHwNumber: 0x05, label: "LeftFlipper")
    .MapKey(ConsoleKey.X,    switchHwNumber: 0x0A, label: "RightFlipper")
    // Inlanes / outlanes
    .MapKey(ConsoleKey.A,    switchHwNumber: 0x02, label: "LeftInlane")
    .MapKey(ConsoleKey.Oem7, switchHwNumber: 0x07, label: "RightInlane")
    .MapKey(ConsoleKey.Q,    switchHwNumber: 0x01, label: "LeftOutlane")
    .MapKey(ConsoleKey.P,    switchHwNumber: 0x06, label: "RightOutlane")
    // Ball path
    .MapKey(ConsoleKey.D,    switchHwNumber: 0x00, label: "ShooterLane")
    // Trough drain simulation
    .MapKey(ConsoleKey.D1,   switchHwNumber: 0x10, label: "Trough0")
    .MapKey(ConsoleKey.D2,   switchHwNumber: 0x11, label: "Trough1")
    .MapKey(ConsoleKey.D3,   switchHwNumber: 0x12, label: "Trough2")
    .MapKey(ConsoleKey.D4,   switchHwNumber: 0x13, label: "Trough3")
    .MapKey(ConsoleKey.D5,   switchHwNumber: 0x14, label: "Trough4")
    // Trough starts full — 5 balls present, NC optos open (beam broken)
    .SetInitialState(0x10, SwitchState.Open)
    .SetInitialState(0x11, SwitchState.Open)
    .SetInitialState(0x12, SwitchState.Open)
    .SetInitialState(0x13, SwitchState.Open)
    .SetInitialState(0x14, SwitchState.Open);

// ── Game ──────────────────────────────────────────────────────────────────────

var game = new SampleGame(machine, platform, loggerFactory);

// ── Media ─────────────────────────────────────────────────────────────────────
// Start UltraPinball.MediaController on port 9000 to receive these events.

using var media = new MediaBridgeClient { Host = "127.0.0.1", Port = 9000, ConnectTimeout = TimeSpan.FromSeconds(2) };
if (await media.ConnectAsync("UltraPinball.Sample", "1.0", "1.0", []))
{
    Console.WriteLine("[Media] Connected to MediaController.");
    game.Media = media;
}
else
{
    Console.WriteLine("[Media] MediaController not found — running without media.");
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await game.RunAsync(cts.Token);
