using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class BallSearchModeTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal game with BallSearchMode active (Ball lifecycle).
    /// Uses a very short timeout (0.05 s) so tests can expire it with Thread.Sleep.
    /// </summary>
    private static (GameController game, PulseCapturingPlatform platform,
                    BallSearchTestMachine machine, BallSearchMode search)
        Build(float timeout = 0.05f)
    {
        var platform = new PulseCapturingPlatform();
        var machine  = new BallSearchTestMachine();
        machine.Initialize(platform);

        var search = new BallSearchMode(
            searchCoilNames: ["Sling1Coil"],
            timeoutSeconds: timeout,
            searchIntervalSeconds: 0.01f);   // short interval so pulses happen quickly in tests

        var game = new GameController(machine, platform, NullLoggerFactory.Instance);
        game.RegisterMode(search);

        game.StartGame();   // Ball=1, BallSearchMode added, ModeStarted called

        return (game, platform, machine, search);
    }

    /// <summary>Advances time past the timeout and ticks the mode queue to fire elapsed delays.</summary>
    private static void ExpireTimeout(GameController game)
    {
        Thread.Sleep(70);          // past the 0.05 s threshold
        game.Modes.Tick(0.1f);    // dispatch elapsed delays
    }

    /// <summary>Fires a switch active event.</summary>
    private static void Activate(GameController game, BallSearchTestMachine machine, string switchName) =>
        game.Modes.HandleSwitchEvent(machine.Switches[switchName], SwitchState.Closed);

    /// <summary>Fires a switch inactive event (Open for NormallyOpen switches).</summary>
    private static void Deactivate(GameController game, BallSearchTestMachine machine, string switchName) =>
        game.Modes.HandleSwitchEvent(machine.Switches[switchName], SwitchState.Open);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Search_StartsAfterIdleTimeout()
    {
        var (game, platform, _, search) = Build();
        var started = false;
        search.BallSearchStarted += () => started = true;

        ExpireTimeout(game);

        Assert.True(search.IsSearching);
        Assert.True(started);
        Assert.Contains(0x04, platform.PulsedCoils);   // Sling1Coil hw number
    }

    [Fact]
    public void PlayfieldSwitch_ResetsIdleTimer()
    {
        var (game, _, machine, search) = Build(timeout: 0.05f);

        // Hit a playfield switch before the timeout fires — this resets the timer.
        Activate(game, machine, "Sling");
        game.Modes.Tick(0.1f);    // dispatch any pending delays

        // Sleep past the original deadline but less than 2× timeout,
        // then tick: the timer was reset so it shouldn't have fired yet.
        Thread.Sleep(40);         // < 0.05 s remaining after the reset
        game.Modes.Tick(0.1f);

        Assert.False(search.IsSearching);
    }

    [Fact]
    public void PlayfieldSwitch_DuringSearch_StopsSearch()
    {
        var (game, _, machine, search) = Build();
        var stopped = false;
        search.BallSearchStopped += () => stopped = true;

        ExpireTimeout(game);
        Assert.True(search.IsSearching);

        Activate(game, machine, "Sling");

        Assert.False(search.IsSearching);
        Assert.True(stopped);
    }

    [Fact]
    public void EosSwitch_ResetsIdleTimer()
    {
        var (game, _, machine, search) = Build(timeout: 0.05f);

        // Hit EOS before the timeout fires — this should reset the timer.
        Activate(game, machine, "LeftEos");
        game.Modes.Tick(0.1f);

        // Same logic as playfield reset test: sleep less than 0.05 s from reset point.
        Thread.Sleep(40);
        game.Modes.Tick(0.1f);

        Assert.False(search.IsSearching);
    }

    [Fact]
    public void ShooterLane_SuspendsTimer()
    {
        var (game, _, machine, search) = Build();

        // Ball arrives in shooter lane — timer should be suspended.
        Activate(game, machine, "ShooterLane");
        game.Modes.Tick(0.1f);

        // Sleep well past the timeout.
        Thread.Sleep(150);
        game.Modes.Tick(0.1f);

        Assert.False(search.IsSearching);
    }

    [Fact]
    public void ShooterLane_Inactive_ResumesTimer()
    {
        var (game, _, machine, search) = Build();

        // Suspend the timer.
        Activate(game, machine, "ShooterLane");
        game.Modes.Tick(0.1f);

        // Ball leaves shooter lane — timer restarts.
        Deactivate(game, machine, "ShooterLane");
        game.Modes.Tick(0.1f);

        // Sleep past the timeout.
        ExpireTimeout(game);

        Assert.True(search.IsSearching);
    }
}

// ── Test machine ──────────────────────────────────────────────────────────────

/// <summary>
/// Minimal machine with one switch of each relevant tag type, plus a coil to pulse.
/// </summary>
class BallSearchTestMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("ShooterLane", hwNumber: 0x00, tags: SwitchTags.ShooterLane);
        AddSwitch("Sling",       hwNumber: 0x01, tags: SwitchTags.Playfield);
        AddSwitch("LeftEos",     hwNumber: 0x02, tags: SwitchTags.Eos);

        AddCoil("Sling1Coil", hwNumber: 0x04, defaultPulseMs: 20);
    }
}

// ── Capturing platform ────────────────────────────────────────────────────────

/// <summary>
/// Records <see cref="PulseCoil"/> calls so ball-search tests can assert
/// that coils were pulsed at the right times.
/// </summary>
class PulseCapturingPlatform : IHardwarePlatform
{
    public List<int> PulsedCoils { get; } = new();

    public event Action<int, SwitchState>? SwitchChanged { add { } remove { } }

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyDictionary<int, SwitchState>> GetInitialSwitchStatesAsync() =>
        Task.FromResult<IReadOnlyDictionary<int, SwitchState>>(new Dictionary<int, SwitchState>());

    public void PulseCoil(int hwNumber, int milliseconds) => PulsedCoils.Add(hwNumber);
    public void HoldCoil(int hwNumber) { }
    public void DisableCoil(int hwNumber) { }
    public void ConfigureFlipperRule(int switchHw, int mainCoilHw, int pulseMs, float holdPower = 0.25f) { }
    public void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs) { }
    public void RemoveHardwareRule(int switchHw) { }
    public void SetLedColor(int hwAddress, byte r, byte g, byte b) { }
    public void SetLedColors(int startAddress, (byte r, byte g, byte b)[] colors) { }
}
