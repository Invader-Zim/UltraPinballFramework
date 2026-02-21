using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class BonusModeTests
{
    // ── Build helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal game with BonusMode active (ball 1 in progress).
    /// StepIntervalSeconds = 0 so each Modes.Tick(0f) advances exactly one step.
    /// </summary>
    private static (GameController game, BonusMode bonus) Build()
    {
        var machine = new EmptyMachine();
        machine.Initialize(new NullPlatform());
        var game  = new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
        var bonus = new BonusMode { StepAmount = 100, StepIntervalSeconds = 0f };
        game.RegisterMode(bonus);
        game.StartGame();   // ball 1 starts → BonusMode.ModeStarted() runs
        return (game, bonus);
    }

    // ── Accumulation ──────────────────────────────────────────────────────────

    [Fact]
    public void AddBonus_AccumulatesValue()
    {
        var (_, bonus) = Build();
        bonus.AddBonus(100);
        bonus.AddBonus(200);
        bonus.AddBonus(50);
        Assert.Equal(350, bonus.BonusValue);
    }

    [Fact]
    public void SetMultiplier_ClampedToOne()
    {
        var (_, bonus) = Build();
        bonus.SetMultiplier(0);
        Assert.Equal(1, bonus.Multiplier);

        bonus.SetMultiplier(-5);
        Assert.Equal(1, bonus.Multiplier);

        bonus.SetMultiplier(3);
        Assert.Equal(3, bonus.Multiplier);
    }

    // ── Countdown ─────────────────────────────────────────────────────────────

    [Fact]
    public void StartBonus_AwardsFullAmount()
    {
        var (game, bonus) = Build();
        bonus.AddBonus(300);  // 3 steps of 100

        bonus.StartBonus();
        game.Modes.Tick(0f);  // step 1: +100
        game.Modes.Tick(0f);  // step 2: +100
        game.Modes.Tick(0f);  // step 3: +100 → Complete → EndBall

        // Score reflects all 300 bonus (player persists across balls)
        Assert.Equal(300, game.CurrentPlayer!.Score);
    }

    [Fact]
    public void Multiplier_ScalesAward()
    {
        var (game, bonus) = Build();
        bonus.AddBonus(100);
        bonus.SetMultiplier(3);  // 100 × 3 = 300 total

        bonus.StartBonus();
        game.Modes.Tick(0f);  // step 1: +100
        game.Modes.Tick(0f);  // step 2: +100
        game.Modes.Tick(0f);  // step 3: +100 → Complete → EndBall

        Assert.Equal(300, game.CurrentPlayer!.Score);
    }

    [Fact]
    public void BonusStep_Event_FiresEachStep()
    {
        var (game, bonus) = Build();
        bonus.AddBonus(300);

        var stepCount = 0;
        bonus.BonusStep += _ => stepCount++;

        bonus.StartBonus();
        game.Modes.Tick(0f);
        game.Modes.Tick(0f);
        game.Modes.Tick(0f);

        Assert.Equal(3, stepCount);
    }

    [Fact]
    public void ZeroBonus_CompletesImmediately()
    {
        var (game, bonus) = Build();
        // BonusValue = 0, no AddBonus call

        long? awarded = null;
        bonus.BonusCompleted += total => awarded = total;

        bonus.StartBonus();  // _remaining == 0 → Complete() called synchronously

        Assert.NotNull(awarded);
        Assert.Equal(0L, awarded);
        Assert.Equal(2, game.Ball);   // EndBall was called → ball advanced to 2
    }

    // ── Integration with TroughMode ───────────────────────────────────────────

    [Fact]
    public void TroughMode_BallDrained_TriggersBonusMode()
    {
        var sim     = new SimulatorPlatform();
        var machine = new TroughTestMachine();
        machine.Initialize(sim);

        var bonus  = new BonusMode { StepAmount = 100, StepIntervalSeconds = 0f };
        var trough = new TroughMode(["Trough0"]);
        trough.BallDrained += bonus.StartBonus;

        var game = new GameController(machine, sim, NullLoggerFactory.Instance);
        game.RegisterMode(trough);
        game.RegisterMode(bonus);

        game.StartGame();
        game.Modes.HandleSwitchEvent(machine.Switches["ShooterLane"], SwitchState.Open);

        bonus.AddBonus(200);  // 2 steps

        // Drain — trough fires BallDrained → bonus.StartBonus() called
        game.Modes.HandleSwitchEvent(machine.Switches["Trough0"], SwitchState.Open);

        game.Modes.Tick(0f);  // step 1: +100
        game.Modes.Tick(0f);  // step 2: +100 → Complete → EndBall

        Assert.Equal(200, game.CurrentPlayer!.Score);
        Assert.Equal(2, game.Ball);   // EndBall fired by BonusMode.Complete
    }
}
