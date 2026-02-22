using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class ServiceModeTests
{
    // ── Test machine ──────────────────────────────────────────────────────────

    private class ServiceTestMachine : MachineConfig
    {
        public override void Configure()
        {
            AddSwitch("ServiceButton", 0x00, tags: SwitchTags.Service);
            AddSwitch("TestSwitch",    0x01, tags: SwitchTags.Playfield);
            AddCoil("TestCoil", hwNumber: 0x01, defaultPulseMs: 20);
        }
    }

    // ── Build helpers ─────────────────────────────────────────────────────────

    private static (GameController game, ServiceMode mode) Build()
    {
        var machine = new ServiceTestMachine();
        machine.Initialize(new NullPlatform());
        var game = new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
        var mode = new ServiceMode();
        game.RegisterMode(mode);
        return (game, mode);
    }

    private static (GameController game, ServiceMode mode, SimulatorPlatform sim) BuildWithSim()
    {
        var sim     = new SimulatorPlatform();
        var machine = new ServiceTestMachine();
        machine.Initialize(sim);
        var game = new GameController(machine, sim, NullLoggerFactory.Instance);
        var mode = new ServiceMode();
        game.RegisterMode(mode);
        return (game, mode, sim);
    }

    private static void FireServiceButton(GameController game) =>
        game.Modes.HandleSwitchEvent(game.Switches["ServiceButton"], SwitchState.Closed);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void EnterService_SetsIsActive()
    {
        var (game, mode) = Build();

        FireServiceButton(game);

        Assert.True(mode.IsActive);
    }

    [Fact]
    public void ExitService_ClearsIsActive()
    {
        var (game, mode) = Build();

        FireServiceButton(game);   // enter
        FireServiceButton(game);   // exit

        Assert.False(mode.IsActive);
    }

    [Fact]
    public void ServiceActive_StopsGameplaySwitches()
    {
        var (game, _) = Build();

        var counter = new SwitchCounterMode();
        game.RegisterMode(counter);

        FireServiceButton(game);   // enter service
        game.Modes.HandleSwitchEvent(game.Switches["TestSwitch"], SwitchState.Closed);

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public void ServiceActive_DisablesCoils()
    {
        var (game, _) = Build();

        FireServiceButton(game);

        Assert.All(game.Coils, coil => Assert.False(coil.IsEnabled));
    }

    [Fact]
    public void ServiceExit_ReEnablesCoils()
    {
        var (game, _) = Build();

        FireServiceButton(game);   // enter
        FireServiceButton(game);   // exit

        Assert.All(game.Coils, coil => Assert.True(coil.IsEnabled));
    }

    [Fact]
    public void TestCoil_PulsesCoil()
    {
        var (game, mode, sim) = BuildWithSim();

        FireServiceButton(game);   // enter service so TestCoil is allowed
        sim.CoilLog.Clear();

        mode.TestCoil("TestCoil");

        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x01"));
    }
}

// ── Helper mode ────────────────────────────────────────────────────────────────

class SwitchCounterMode : Mode
{
    public int Count { get; private set; }
    public SwitchCounterMode() : base(priority: 1) { }
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    public override void ModeStarted() =>
        AddSwitchHandler("TestSwitch", SwitchActivation.Active, _ =>
        {
            Count++;
            return SwitchHandlerResult.Continue;
        });
}
