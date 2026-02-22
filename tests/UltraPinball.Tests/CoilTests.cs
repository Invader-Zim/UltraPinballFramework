using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class CoilTests
{
    // ── Build helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal game and returns the one coil declared in OneCoilMachine.
    /// The game is started so the coil is fully attached to the platform.
    /// </summary>
    private static (GameController game, SimulatorPlatform sim, Coil coil) Build()
    {
        var sim     = new SimulatorPlatform();
        var machine = new OneCoilMachine();
        machine.Initialize(sim);
        var game = new GameController(machine, sim, NullLoggerFactory.Instance);
        return (game, sim, machine.Coils["TestCoil"]);
    }

    // ── Software gate ─────────────────────────────────────────────────────────

    [Fact]
    public void Coil_EnabledByDefault_PulseGoesThrough()
    {
        var (_, sim, coil) = Build();
        Assert.True(coil.IsEnabled);

        coil.Pulse();

        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x01"));
    }

    [Fact]
    public void Pulse_IsNoOp_WhenDisabled()
    {
        var (_, sim, coil) = Build();
        coil.Disable();
        sim.CoilLog.Clear();

        coil.Pulse();

        Assert.DoesNotContain(sim.CoilLog, e => e.Contains("PULSE"));
    }

    [Fact]
    public void Hold_IsNoOp_WhenDisabled()
    {
        var (_, sim, coil) = Build();
        coil.Disable();
        sim.CoilLog.Clear();

        coil.Hold();

        Assert.DoesNotContain(sim.CoilLog, e => e.Contains("HOLD"));
    }

    [Fact]
    public void Enable_ReallowsPulse()
    {
        var (_, sim, coil) = Build();
        coil.Disable();
        coil.Enable();
        sim.CoilLog.Clear();

        coil.Pulse();

        Assert.True(coil.IsEnabled);
        Assert.Contains(sim.CoilLog, e => e.Contains("PULSE") && e.Contains("0x01"));
    }

    [Fact]
    public void Disable_CutsPower()
    {
        var (_, sim, coil) = Build();

        coil.Disable();

        Assert.False(coil.IsEnabled);
        Assert.Contains(sim.CoilLog, e => e.Contains("DISABLE") && e.Contains("0x01"));
    }

    // ── Timed hold ────────────────────────────────────────────────────────────

    [Fact]
    public void HoldCoilFor_ReleasesAfterDuration()
    {
        var (game, sim, coil) = Build();

        // Use a minimal mode to access HoldCoilFor
        var mode = new HoldTestMode();
        game.RegisterMode(mode);

        sim.CoilLog.Clear();
        mode.TriggerHold("TestCoil", 0f);  // 0-second delay → fires on next Tick

        game.Modes.Tick(0f);               // dispatch the release delay

        Assert.Contains(sim.CoilLog, e => e.Contains("HOLD")    && e.Contains("0x01"));
        Assert.Contains(sim.CoilLog, e => e.Contains("DISABLE")  && e.Contains("0x01"));
        Assert.True(coil.IsEnabled);       // IsEnabled must remain true after release
    }
}

// ── Test-only helper mode ──────────────────────────────────────────────────────

class HoldTestMode : Mode
{
    public HoldTestMode() : base(priority: 50) { }
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    public void TriggerHold(string coilName, float seconds) =>
        HoldCoilFor(coilName, seconds);
}

// ── Minimal one-coil machine ───────────────────────────────────────────────────

class OneCoilMachine : MachineConfig
{
    public override void Configure()
    {
        AddCoil("TestCoil", hwNumber: 0x01, defaultPulseMs: 20);
    }
}
