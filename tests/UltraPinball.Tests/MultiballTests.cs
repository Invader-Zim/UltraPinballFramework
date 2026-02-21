using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class MultiballTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a game with TroughMode active and the first ball confirmed in play.
    /// StartGame() → BallStarting fires → ShooterLane goes inactive → _ballsInPlay == 1.
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
        game.RegisterMode(trough);

        game.StartGame();

        // Confirm first ball in play
        game.Modes.HandleSwitchEvent(machine.Switches["ShooterLane"], SwitchState.Open);

        sim.CoilLog.Clear();
        return (game, sim, machine, trough);
    }

    /// <summary>
    /// Ejects a second ball and confirms it in play via the shooter-lane switch.
    /// After this call, trough.BallsInPlay == 2.
    /// </summary>
    private static void AddBallAndConfirm(GameController game, TroughTestMachine machine, TroughMode trough)
    {
        trough.AddBall();
        game.Modes.HandleSwitchEvent(machine.Switches["ShooterLane"], SwitchState.Open);
    }

    private static void Drain(GameController game, TroughTestMachine machine) =>
        game.Modes.HandleSwitchEvent(machine.Switches["Trough0"], SwitchState.Open);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddBall_IncreasesBallsInPlay()
    {
        var (game, _, machine, trough) = Build();
        Assert.Equal(1, trough.BallsInPlay);
        Assert.False(trough.IsMultiBallActive);

        AddBallAndConfirm(game, machine, trough);

        Assert.Equal(2, trough.BallsInPlay);
        Assert.True(trough.IsMultiBallActive);
    }

    [Fact]
    public void MultiBallStarted_FiredWhenSecondBallEntersPlay()
    {
        var (game, _, machine, trough) = Build();
        var fired = false;
        trough.MultiBallStarted += () => fired = true;

        trough.AddBall();
        Assert.False(fired); // not yet — shooter lane hasn't gone inactive

        game.Modes.HandleSwitchEvent(machine.Switches["ShooterLane"], SwitchState.Open);

        Assert.True(fired);
    }

    [Fact]
    public void MultiballDrain_DecrementsBallsInPlay_DoesNotEndBall()
    {
        var (game, _, machine, trough) = Build();
        AddBallAndConfirm(game, machine, trough);
        Assert.Equal(2, trough.BallsInPlay);

        Drain(game, machine);

        // Ball 1 is still in progress — EndBall was not called
        Assert.Equal(1, game.Ball);
        Assert.Equal(1, trough.BallsInPlay);
        Assert.False(trough.IsMultiBallActive);
    }

    [Fact]
    public void MultiBallEnded_FiredWhenReturnToSingleBall()
    {
        var (game, _, machine, trough) = Build();
        AddBallAndConfirm(game, machine, trough);

        var fired = false;
        trough.MultiBallEnded += () => fired = true;

        Drain(game, machine);

        Assert.True(fired);
    }

    [Fact]
    public void LastBallDrain_DuringMultiball_EndsBall()
    {
        var (game, _, machine, trough) = Build();
        AddBallAndConfirm(game, machine, trough);

        // Drain first ball
        Drain(game, machine);
        Assert.Equal(1, trough.BallsInPlay);

        // Drain second (last) ball
        Drain(game, machine);

        // EndBall was called; single-player game advanced to ball 2
        Assert.Equal(2, game.Ball);
    }

    [Fact]
    public void BallSave_AppliesOnlyToLastBallDrain()
    {
        var (game, sim, machine, trough) = Build();
        AddBallAndConfirm(game, machine, trough);
        sim.CoilLog.Clear();

        trough.StartBallSave(30f);

        // Drain one ball — multiball drain, save does NOT apply
        Drain(game, machine);
        Assert.Equal(1, trough.BallsInPlay);
        Assert.Empty(sim.CoilLog);       // no re-eject
        Assert.Equal(1, game.Ball);      // ball still in progress

        // Drain the last ball — save DOES apply
        Drain(game, machine);
        Assert.Equal(1, game.Ball);      // still ball 1 — re-ejected
        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x06"));
    }
}
