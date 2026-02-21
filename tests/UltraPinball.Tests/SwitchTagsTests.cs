using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;

namespace UltraPinball.Tests;

public class SwitchTagsTests
{
    [Fact]
    public void Switch_Tags_DefaultsToNone()
    {
        var sw = new Switch("X", 0);
        Assert.Equal(SwitchTags.None, sw.Tags);
    }

    [Fact]
    public void Switch_Tags_StoredFromConstructor()
    {
        var sw = new Switch("X", 0, tags: SwitchTags.Playfield);
        Assert.Equal(SwitchTags.Playfield, sw.Tags);
    }

    [Fact]
    public void AddSwitch_Tags_QueryableByFlag()
    {
        var machine = new TaggedTestMachine();
        machine.Initialize(new NullPlatform());

        var playfieldSwitches = machine.Switches.Where(sw => sw.Tags.HasFlag(SwitchTags.Playfield)).ToList();
        Assert.Single(playfieldSwitches);
        Assert.Equal("Sling", playfieldSwitches[0].Name);
    }
}

// ── Minimal test machine ──────────────────────────────────────────────────────

class TaggedTestMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("Sling", hwNumber: 0x01, tags: SwitchTags.Playfield);
        AddSwitch("Tilt",  hwNumber: 0x02, tags: SwitchTags.Tilt);
    }
}

// ── Null platform (no-op) ─────────────────────────────────────────────────────

class NullPlatform : IHardwarePlatform
{
    public event Action<int, SwitchState>? SwitchChanged { add { } remove { } }

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyDictionary<int, SwitchState>> GetInitialSwitchStatesAsync() =>
        Task.FromResult<IReadOnlyDictionary<int, SwitchState>>(new Dictionary<int, SwitchState>());

    public void PulseCoil(int hwNumber, int milliseconds) { }
    public void HoldCoil(int hwNumber) { }
    public void DisableCoil(int hwNumber) { }
    public void ConfigureFlipperRule(int switchHw, int mainCoilHw, int pulseMs, float holdPower = 0.25f) { }
    public void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs) { }
    public void RemoveHardwareRule(int switchHw) { }
    public void SetLedColor(int hwAddress, byte r, byte g, byte b) { }
    public void SetLedColors(int startAddress, (byte r, byte g, byte b)[] colors) { }
}
