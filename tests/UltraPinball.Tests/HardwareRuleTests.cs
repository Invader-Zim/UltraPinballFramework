using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;
using UltraPinball.Simulator;

namespace UltraPinball.Tests;

public class HardwareRuleTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (SimulatorPlatform sim, RuleMachine machine) BuildSim()
    {
        var sim     = new SimulatorPlatform();
        var machine = new RuleMachine();
        machine.Initialize(sim);
        return (sim, machine);
    }

    private static (RecordingPlatform rec, RuleMachine machine) BuildRecording()
    {
        var rec     = new RecordingPlatform();
        var machine = new RuleMachine();
        machine.Initialize(rec);
        return (rec, machine);
    }

    // ── MachineConfig → Platform wiring ──────────────────────────────────────

    [Fact]
    public void FlipperRule_CallsPlatformWithCorrectHwNumbers()
    {
        var (rec, _) = BuildRecording();

        Assert.Single(rec.FlipperRules);
        var rule = rec.FlipperRules[0];
        Assert.Equal(0x05, rule.SwitchHw);
        Assert.Equal(0x00, rule.MainCoilHw);
        Assert.Equal(30,   rule.PulseMs);
        Assert.Equal(0.25f, rule.HoldPower);
    }

    [Fact]
    public void BumperRule_CallsPlatformWithCorrectHwNumbers()
    {
        var (rec, _) = BuildRecording();

        Assert.Single(rec.BumperRules);
        var rule = rec.BumperRules[0];
        Assert.Equal(0x04, rule.SwitchHw);
        Assert.Equal(0x04, rule.CoilHw);
        Assert.Equal(20,   rule.PulseMs);
    }

    [Fact]
    public void RemoveHardwareRule_CallsPlatform()
    {
        var (rec, machine) = BuildRecording();

        machine.RemoveFlipperRulePublic("LeftFlipper");

        Assert.Contains(0x05, rec.RemovedRules);
    }

    // ── Simulator enforcement ─────────────────────────────────────────────────

    [Fact]
    public void Simulator_FlipperRule_PulsesMainCoilOnClose()
    {
        var (sim, _) = BuildSim();

        sim.TriggerSwitch(0x05, SwitchState.Closed);

        Assert.Contains(sim.CoilLog, e => e == "PULSE 0x00 30ms");
    }

    [Fact]
    public void Simulator_FlipperRule_DisablesCoilOnOpen()
    {
        var (sim, _) = BuildSim();

        sim.TriggerSwitch(0x05, SwitchState.Closed);
        sim.CoilLog.Clear();
        sim.TriggerSwitch(0x05, SwitchState.Open);

        Assert.Contains(sim.CoilLog, e => e == "DISABLE 0x00");
    }

    [Fact]
    public void Simulator_BumperRule_PulsesCoilOnClose()
    {
        var (sim, _) = BuildSim();

        sim.TriggerSwitch(0x04, SwitchState.Closed);

        Assert.Contains(sim.CoilLog, e => e == "PULSE 0x04 20ms");
    }
}

// ── Test machine ──────────────────────────────────────────────────────────────

/// <summary>
/// Declares a flipper (sw=0x05 → coil=0x00) and a sling (sw=0x04 → coil=0x04).
/// Exposes RemoveFlipperRulePublic so tests can verify RemoveHardwareRule.
/// </summary>
class RuleMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("LeftFlipper", hwNumber: 0x05, debounce: false);
        AddSwitch("LeftSling",   hwNumber: 0x04);

        AddCoil("LeftFlipperMain", hwNumber: 0x00, defaultPulseMs: 30);
        AddCoil("LeftSlingCoil",   hwNumber: 0x04, defaultPulseMs: 20);

        AddFlipperRule("LeftFlipper", mainCoil: "LeftFlipperMain", pulseMs: 30, holdPower: 0.25f);
        AddBumperRule("LeftSling",    coilName: "LeftSlingCoil",   pulseMs: 20);
    }

    // Exposes the protected helper for the RemoveHardwareRule test.
    public void RemoveFlipperRulePublic(string switchName) => RemoveHardwareRule(switchName);
}

// ── Recording platform ────────────────────────────────────────────────────────

class RecordingPlatform : IHardwarePlatform
{
    public record FlipperRuleCall(int SwitchHw, int MainCoilHw, int PulseMs, float HoldPower);
    public record BumperRuleCall(int SwitchHw, int CoilHw, int PulseMs);

    public List<FlipperRuleCall> FlipperRules { get; } = new();
    public List<BumperRuleCall>  BumperRules  { get; } = new();
    public List<int>             RemovedRules { get; } = new();

    public event Action<int, SwitchState>? SwitchChanged { add { } remove { } }

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyDictionary<int, SwitchState>> GetInitialSwitchStatesAsync() =>
        Task.FromResult<IReadOnlyDictionary<int, SwitchState>>(new Dictionary<int, SwitchState>());

    public void PulseCoil(int hwNumber, int milliseconds) { }
    public void HoldCoil(int hwNumber) { }
    public void DisableCoil(int hwNumber) { }

    public void ConfigureFlipperRule(int switchHw, int mainCoilHw, int pulseMs, float holdPower = 0.25f) =>
        FlipperRules.Add(new FlipperRuleCall(switchHw, mainCoilHw, pulseMs, holdPower));

    public void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs) =>
        BumperRules.Add(new BumperRuleCall(switchHw, coilHw, pulseMs));

    public void RemoveHardwareRule(int switchHw) =>
        RemovedRules.Add(switchHw);

    public void SetLedColor(int hwAddress, byte r, byte g, byte b) { }
    public void SetLedColors(int startAddress, (byte r, byte g, byte b)[] colors) { }
}
