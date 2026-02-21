using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class TroughModeTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a game with TroughMode active and the ball "in play":
    /// StartGame() → BallStarting fires → ShooterLane goes inactive → _ballInPlay = true.
    /// </summary>
    private static (GameController game, SimulatorPlatform sim,
                    TroughTestMachine machine, TroughMode trough) Build(
                    float autoSaveSeconds = 0f)
    {
        var sim     = new SimulatorPlatform();
        var machine = new TroughTestMachine();
        machine.Initialize(sim);

        var trough = new TroughMode(["Trough0"]) { AutoBallSaveSeconds = autoSaveSeconds };
        var game   = new GameController(machine, sim, NullLoggerFactory.Instance);
        game.RegisterMode(trough);   // System lifecycle → added immediately, ModeStarted called

        game.StartGame();            // Ball=1, IsGameInProgress=true, BallStarting fires → _launching=true

        // Simulate ball leaving the shooter lane → _launching=false, _ballInPlay=true
        game.Modes.HandleSwitchEvent(machine.Switches["ShooterLane"], SwitchState.Open);

        sim.CoilLog.Clear();         // clear eject log from startup
        return (game, sim, machine, trough);
    }

    /// <summary>Simulates a drain: Trough0 is NormallyClosed → Active = Open (beam broken).</summary>
    private static void Drain(GameController game, TroughTestMachine machine) =>
        game.Modes.HandleSwitchEvent(machine.Switches["Trough0"], SwitchState.Open);

    // ── Ball save state ───────────────────────────────────────────────────────

    [Fact]
    public void BallSave_ActiveAfterStart()
    {
        var (_, _, _, trough) = Build();
        trough.StartBallSave(5f);
        Assert.True(trough.BallSaveActive);
    }

    [Fact]
    public void BallSave_InactiveAfterStop()
    {
        var (_, _, _, trough) = Build();
        trough.StartBallSave(5f);
        trough.StopBallSave();
        Assert.False(trough.BallSaveActive);
    }

    [Fact]
    public void BallSave_ExpiresAfterDuration()
    {
        var (game, _, _, trough) = Build();
        var expired = false;
        trough.BallSaveExpired += () => expired = true;

        trough.StartBallSave(0.05f);
        Thread.Sleep(80);           // past the 50 ms threshold
        game.Modes.Tick(0.1f);      // dispatch the elapsed delay

        Assert.False(trough.BallSaveActive);
        Assert.True(expired);
    }

    // ── Ball save on drain ────────────────────────────────────────────────────

    [Fact]
    public void BallSave_SavesBallOnDrain()
    {
        var (game, sim, machine, trough) = Build();
        var saved = false;
        trough.BallSaved += () => saved = true;

        trough.StartBallSave(5f);
        Drain(game, machine);

        Assert.True(saved);
        Assert.Equal(1, game.Ball);   // EndBall was NOT called — still on ball 1
        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x06")); // TroughEject pulsed
    }

    [Fact]
    public void BallSave_DoesNotSave_WhenExpired()
    {
        var (game, _, machine, trough) = Build();

        trough.StartBallSave(0.05f);
        Thread.Sleep(80);
        game.Modes.Tick(0.1f);  // expire the save

        Drain(game, machine);

        // EndBall was called: 1 player, ball advanced from 1 → StartBall for ball 2
        Assert.Equal(2, game.Ball);
    }

    [Fact]
    public void BallSave_DoesNotSave_WhenStopped()
    {
        var (game, _, machine, trough) = Build();

        trough.StartBallSave(5f);
        trough.StopBallSave();

        Drain(game, machine);

        Assert.Equal(2, game.Ball);
    }

    // ── Auto ball save ────────────────────────────────────────────────────────

    [Fact]
    public void AutoBallSave_StartsOnBallStarting()
    {
        // Build() calls StartGame() which fires BallStarting; with autoSaveSeconds=5f
        // TroughMode should automatically open the save window.
        var (_, _, _, trough) = Build(autoSaveSeconds: 5f);
        Assert.True(trough.BallSaveActive);
    }
}

// ── Test machine ──────────────────────────────────────────────────────────────

class TroughTestMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("ShooterLane", hwNumber: 0x00);                                   // NormallyOpen
        AddSwitch("Trough0",     hwNumber: 0x10, type: SwitchType.NormallyClosed);  // NC opto
        AddCoil("TroughEject",   hwNumber: 0x06, defaultPulseMs: 15);
    }
}
