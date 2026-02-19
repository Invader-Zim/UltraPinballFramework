using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class ModeQueueTests
{
    private static (GameController game, TestMachine machine) Build()
    {
        var sim = new SimulatorPlatform();
        var machine = new TestMachine();
        machine.Initialize(sim); // internal, visible via InternalsVisibleTo
        var game = new GameController(machine, sim, NullLoggerFactory.Instance);
        return (game, machine);
    }

    [Fact]
    public void SwitchEvent_RoutedToActiveMode()
    {
        var (game, machine) = Build();
        var log = new List<string>();
        game.Modes.Add(new LoggingMode(10, log));

        game.Modes.HandleSwitchEvent(machine.Switches["TestSwitch"], SwitchState.Closed);

        Assert.Contains("TestSwitch:Closed", log);
    }

    [Fact]
    public void HigherPriorityMode_CanStopEvent()
    {
        var (game, machine) = Build();
        var log = new List<string>();
        game.Modes.Add(new StopAllMode(priority: 100));
        game.Modes.Add(new LoggingMode(priority: 1, log));

        game.Modes.HandleSwitchEvent(machine.Switches["TestSwitch"], SwitchState.Closed);

        Assert.Empty(log); // low-priority mode should not have seen the event
    }

    [Fact]
    public void LowerPriorityMode_SeesEvent_WhenHigherContinues()
    {
        var (game, machine) = Build();
        var log = new List<string>();
        game.Modes.Add(new PassThroughMode(priority: 100));
        game.Modes.Add(new LoggingMode(priority: 1, log));

        game.Modes.HandleSwitchEvent(machine.Switches["TestSwitch"], SwitchState.Closed);

        Assert.Contains("TestSwitch:Closed", log);
    }

    [Fact]
    public void SwitchIsActive_NormallyOpen()
    {
        var sw = new Switch("Test", 0x00, SwitchType.NormallyOpen);
        sw.State = SwitchState.Open;
        Assert.False(sw.IsActive);
        sw.State = SwitchState.Closed;
        Assert.True(sw.IsActive);
    }

    [Fact]
    public void SwitchIsActive_NormallyClosed()
    {
        var sw = new Switch("Test", 0x00, SwitchType.NormallyClosed);
        sw.State = SwitchState.Closed; // resting state — NOT active
        Assert.False(sw.IsActive);
        sw.State = SwitchState.Open;   // ball broke the beam — active
        Assert.True(sw.IsActive);
    }

    [Fact]
    public void Mode_NotAddedTwice()
    {
        var (game, _) = Build();
        var mode = new PassThroughMode(10);
        game.Modes.Add(mode);
        Assert.Throws<InvalidOperationException>(() => game.Modes.Add(mode));
    }

    [Fact]
    public void Delay_FiresAfterElapsed()
    {
        var (game, machine) = Build();
        var fired = false;
        var mode = new DelayTestMode(10, () => fired = true, delaySeconds: 0.001f);
        game.Modes.Add(mode);

        // Manually trigger to set up the delay, then tick past it
        game.Modes.HandleSwitchEvent(machine.Switches["TestSwitch"], SwitchState.Closed);
        Thread.Sleep(10); // ensure delay has elapsed
        game.Modes.Tick(0.1f);

        Assert.True(fired);
    }
}

// ── Test machine ──────────────────────────────────────────────────────────────

class TestMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("TestSwitch",  hwNumber: 0x00);
        AddSwitch("StartButton", hwNumber: 0x10);
    }
}

// ── Test mode doubles ─────────────────────────────────────────────────────────

class LoggingMode(int priority, List<string> log) : Mode(priority)
{
    public override void ModeStarted()
    {
        AddSwitchHandler("TestSwitch", SwitchActivation.Closed,
            sw => { log.Add($"{sw.Name}:Closed"); return SwitchHandlerResult.Continue; });
    }
}

class StopAllMode(int priority) : Mode(priority)
{
    public override void ModeStarted()
    {
        AddSwitchHandler("TestSwitch", SwitchActivation.Closed, _ => SwitchHandlerResult.Stop);
    }
}

class PassThroughMode(int priority) : Mode(priority)
{
    public override void ModeStarted()
    {
        AddSwitchHandler("TestSwitch", SwitchActivation.Closed, _ => SwitchHandlerResult.Continue);
    }
}

class DelayTestMode(int priority, Action callback, float delaySeconds) : Mode(priority)
{
    public override void ModeStarted()
    {
        AddSwitchHandler("TestSwitch", SwitchActivation.Closed, _ =>
        {
            Delay(delaySeconds, callback);
            return SwitchHandlerResult.Continue;
        });
    }
}
