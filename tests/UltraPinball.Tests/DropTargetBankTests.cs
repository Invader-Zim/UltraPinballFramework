using UltraPinball.Core.Game;
using UltraPinball.Core.Devices;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class DropTargetBankTests
{
    // ── Build helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a started game with a three-target bank (Target0/1/2) and a reset coil.
    /// Ball 1 is in progress so the Ball-lifecycle bank is active.
    /// </summary>
    private static (GameController game, SimulatorPlatform sim,
                    DropTargetMachine machine, DropTargetBank bank) Build(
        float autoResetSeconds = 0f)
    {
        var sim     = new SimulatorPlatform();
        var machine = new DropTargetMachine();
        machine.Initialize(sim);
        var game = new GameController(machine, sim, NullLoggerFactory.Instance);
        var bank = new DropTargetBank(
            ["Target0", "Target1", "Target2"],
            resetCoilName: "DropReset",
            autoResetSeconds: autoResetSeconds);
        game.RegisterMode(bank);
        game.StartGame();   // ball 1 starts → bank.ModeStarted() runs
        return (game, sim, machine, bank);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TargetHit_FiresEventWithSwitchName()
    {
        var (game, _, machine, bank) = Build();
        string? fired = null;
        bank.TargetHit += n => fired = n;

        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);

        Assert.Equal("Target0", fired);
    }

    [Fact]
    public void AllTargetsDown_FiresWhenLastTargetHit()
    {
        var (game, _, machine, bank) = Build();
        var completedCount = 0;
        bank.AllTargetsDown += () => completedCount++;

        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);
        game.Modes.HandleSwitchEvent(machine.Switches["Target1"], SwitchState.Closed);
        Assert.Equal(0, completedCount);  // not yet

        game.Modes.HandleSwitchEvent(machine.Switches["Target2"], SwitchState.Closed);
        Assert.Equal(1, completedCount);
    }

    [Fact]
    public void IsComplete_FalseUntilAllDown_ThenTrue()
    {
        var (game, _, machine, bank) = Build();

        Assert.False(bank.IsComplete);

        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);
        Assert.False(bank.IsComplete);
        Assert.Equal(1, bank.DroppedCount);

        game.Modes.HandleSwitchEvent(machine.Switches["Target1"], SwitchState.Closed);
        Assert.False(bank.IsComplete);
        Assert.Equal(2, bank.DroppedCount);

        game.Modes.HandleSwitchEvent(machine.Switches["Target2"], SwitchState.Closed);
        Assert.True(bank.IsComplete);
        Assert.Equal(3, bank.DroppedCount);
    }

    [Fact]
    public void Reset_PulsesCoilAndClearsState()
    {
        var (game, sim, machine, bank) = Build();

        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);
        game.Modes.HandleSwitchEvent(machine.Switches["Target1"], SwitchState.Closed);
        game.Modes.HandleSwitchEvent(machine.Switches["Target2"], SwitchState.Closed);
        Assert.True(bank.IsComplete);

        sim.CoilLog.Clear();
        bank.Reset();

        Assert.Equal(0, bank.DroppedCount);
        Assert.False(bank.IsComplete);
        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x20"));
    }

    [Fact]
    public void AutoReset_PulsesCoilAfterDelay()
    {
        // autoResetSeconds = 0.001f → delay is scheduled; fires on Tick after sleep
        var (game, sim, machine, bank) = Build(autoResetSeconds: 0.001f);

        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);
        game.Modes.HandleSwitchEvent(machine.Switches["Target1"], SwitchState.Closed);
        game.Modes.HandleSwitchEvent(machine.Switches["Target2"], SwitchState.Closed);
        Assert.True(bank.IsComplete);

        sim.CoilLog.Clear();
        System.Threading.Thread.Sleep(5);  // ensure 0.001 s has elapsed
        game.Modes.Tick(0f);               // dispatch the pending auto-reset delay

        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x20"));
        Assert.Equal(0, bank.DroppedCount);
    }

    [Fact]
    public void TargetHit_IsIdempotent()
    {
        var (game, _, machine, bank) = Build();
        var hitCount = 0;
        bank.TargetHit += _ => hitCount++;

        // Fire the same target switch twice (simulates switch bounce)
        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);
        game.Modes.HandleSwitchEvent(machine.Switches["Target0"], SwitchState.Closed);

        Assert.Equal(1, hitCount);
        Assert.Equal(1, bank.DroppedCount);
    }
}

// ── Minimal drop-target machine ───────────────────────────────────────────────

class DropTargetMachine : UltraPinball.Core.Game.MachineConfig
{
    public override void Configure()
    {
        AddSwitch("Target0", hwNumber: 0x10);
        AddSwitch("Target1", hwNumber: 0x11);
        AddSwitch("Target2", hwNumber: 0x12);
        AddCoil("DropReset", hwNumber: 0x20, defaultPulseMs: 30);
    }
}
