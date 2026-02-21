using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class TiltModeTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<FlipperConfig> TestFlippers =
    [
        new FlipperConfig("LeftFlipper",  "LeftFlipperMain",  PulseMs: 30),
        new FlipperConfig("RightFlipper", "RightFlipperMain", PulseMs: 30),
    ];

    /// <summary>
    /// Builds a minimal game with TiltMode active (Ball lifecycle → added on StartGame).
    /// Uses a short cooldown (50 ms) so tests can expire it with a brief Thread.Sleep.
    /// </summary>
    private static (GameController game, TiltCapturingPlatform platform,
                    TiltTestMachine machine, TiltMode tilt)
        Build(int warningsAllowed = 2, IReadOnlyList<FlipperConfig>? flippers = null)
    {
        var platform = new TiltCapturingPlatform();
        var machine  = new TiltTestMachine();
        machine.Initialize(platform);

        var tilt = new TiltMode(
            warningsAllowed: warningsAllowed,
            flippers: flippers,
            cooldownSeconds: 0.05f);   // short so tests don't block for 500 ms

        var game = new GameController(machine, platform, NullLoggerFactory.Instance);
        game.RegisterMode(tilt);       // Ball lifecycle — added when StartGame → StartBall fires

        game.StartGame();              // Ball=1, TiltMode added, ModeStarted called

        return (game, platform, machine, tilt);
    }

    /// <summary>Activates the tilt-bob switch (NormallyOpen → Active = Closed).</summary>
    private static void HitTilt(GameController game, TiltTestMachine machine) =>
        game.Modes.HandleSwitchEvent(machine.Switches["Tilt"], SwitchState.Closed);

    /// <summary>Waits for the 50 ms cooldown to elapse and dispatches it.</summary>
    private static void ExpireCooldown(GameController game)
    {
        Thread.Sleep(70);          // past the 50 ms threshold
        game.Modes.Tick(0.1f);    // dispatch the elapsed delay
    }

    /// <summary>Hits tilt and then expires the cooldown (ready for the next hit).</summary>
    private static void HitTiltAndExpireCooldown(GameController game, TiltTestMachine machine)
    {
        HitTilt(game, machine);
        ExpireCooldown(game);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TiltWarning_FiresOnFirstHit()
    {
        var (game, _, machine, tilt) = Build(warningsAllowed: 2);
        var warnings = new List<int>();
        tilt.TiltWarning += w => warnings.Add(w);

        HitTilt(game, machine);

        Assert.Equal([1], warnings);
        Assert.False(tilt.IsTilted);
    }

    [Fact]
    public void Tilt_OccursAfterExceedingWarnings()
    {
        var (game, _, machine, tilt) = Build(warningsAllowed: 2);

        HitTiltAndExpireCooldown(game, machine);  // warning 1
        HitTiltAndExpireCooldown(game, machine);  // warning 2
        HitTilt(game, machine);                   // tilt!

        Assert.True(tilt.IsTilted);
    }

    [Fact]
    public void TiltWarning_CooldownIgnoresRapidHits()
    {
        var (game, _, machine, tilt) = Build(warningsAllowed: 2);
        var warningCount = 0;
        tilt.TiltWarning += _ => warningCount++;

        HitTilt(game, machine);   // first hit — warning 1, cooldown starts
        HitTilt(game, machine);   // rapid second hit — should be ignored

        Assert.Equal(1, warningCount);
        Assert.Equal(1, tilt.WarningCount);
    }

    [Fact]
    public void Tilt_FlipperRulesRemoved()
    {
        var (game, platform, machine, _) = Build(warningsAllowed: 2, flippers: TestFlippers);

        HitTiltAndExpireCooldown(game, machine);
        HitTiltAndExpireCooldown(game, machine);
        HitTilt(game, machine);  // tilt

        Assert.Contains(0x05, platform.RemovedRules);  // LeftFlipper hw
        Assert.Contains(0x0A, platform.RemovedRules);  // RightFlipper hw
    }

    [Fact]
    public void Tilt_FlipperRulesRestoredOnBallEnd()
    {
        var (game, platform, machine, _) = Build(warningsAllowed: 2, flippers: TestFlippers);

        HitTiltAndExpireCooldown(game, machine);
        HitTiltAndExpireCooldown(game, machine);
        HitTilt(game, machine);  // tilt

        platform.ConfiguredFlipperRules.Clear();  // ignore any earlier calls
        game.EndBall();                           // ModeStopped → restores flipper rules

        Assert.Contains(platform.ConfiguredFlipperRules,
            r => r.SwitchHw == 0x05 && r.CoilHw == 0x00 && r.PulseMs == 30);
        Assert.Contains(platform.ConfiguredFlipperRules,
            r => r.SwitchHw == 0x0A && r.CoilHw == 0x02 && r.PulseMs == 30);
    }

    [Fact]
    public void Tilt_IgnoresFurtherHitsAfterTilt()
    {
        var (game, _, machine, tilt) = Build(warningsAllowed: 2);

        HitTiltAndExpireCooldown(game, machine);
        HitTiltAndExpireCooldown(game, machine);
        HitTilt(game, machine);  // tilt — warningCount is now 3

        var countAfterTilt = tilt.WarningCount;
        HitTilt(game, machine);  // should be ignored
        HitTilt(game, machine);

        Assert.Equal(countAfterTilt, tilt.WarningCount);
    }

    [Fact]
    public void SlamTilt_EndsGame()
    {
        var (game, _, machine, tilt) = Build();
        var slamFired = false;
        tilt.SlamTilted += () => slamFired = true;

        game.Modes.HandleSwitchEvent(machine.Switches["SlamTilt"], SwitchState.Closed);

        Assert.True(slamFired);
        Assert.False(game.IsGameInProgress);
    }

    [Fact]
    public void TiltState_ResetsEachBall()
    {
        var (game, _, machine, tilt) = Build(warningsAllowed: 2);

        // Tilt ball 1
        HitTiltAndExpireCooldown(game, machine);
        HitTiltAndExpireCooldown(game, machine);
        HitTilt(game, machine);
        Assert.True(tilt.IsTilted);

        // End ball 1 — ModeStopped, then StartBall 2 → ModeStarted resets state
        game.EndBall();

        Assert.False(tilt.IsTilted);
        Assert.Equal(0, tilt.WarningCount);
    }
}

// ── Test machine ──────────────────────────────────────────────────────────────

/// <summary>
/// Minimal machine with tilt switches and both flipper switches + coils,
/// so TiltMode can look up hw numbers when removing / restoring rules.
/// No hardware rules are pre-configured — TiltMode owns that lifecycle.
/// </summary>
class TiltTestMachine : MachineConfig
{
    public override void Configure()
    {
        AddSwitch("Tilt",           hwNumber: 0x0C, tags: SwitchTags.Tilt);
        AddSwitch("SlamTilt",       hwNumber: 0x0D, tags: SwitchTags.SlamTilt);
        AddSwitch("LeftFlipper",    hwNumber: 0x05, debounce: false, tags: SwitchTags.UserButton);
        AddSwitch("RightFlipper",   hwNumber: 0x0A, debounce: false, tags: SwitchTags.UserButton);

        AddCoil("LeftFlipperMain",  hwNumber: 0x00, defaultPulseMs: 30);
        AddCoil("RightFlipperMain", hwNumber: 0x02, defaultPulseMs: 30);
    }
}

// ── Capturing platform ────────────────────────────────────────────────────────

/// <summary>
/// Records <see cref="RemoveHardwareRule"/> and <see cref="ConfigureFlipperRule"/> calls
/// so tilt tests can assert that rules were removed and restored at the right times.
/// </summary>
class TiltCapturingPlatform : IHardwarePlatform
{
    public record FlipperRuleCall(int SwitchHw, int CoilHw, int PulseMs, float HoldPower);

    public List<int>             RemovedRules          { get; } = new();
    public List<FlipperRuleCall> ConfiguredFlipperRules { get; } = new();

    public event Action<int, SwitchState>? SwitchChanged { add { } remove { } }

    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<IReadOnlyDictionary<int, SwitchState>> GetInitialSwitchStatesAsync() =>
        Task.FromResult<IReadOnlyDictionary<int, SwitchState>>(new Dictionary<int, SwitchState>());

    public void PulseCoil(int hwNumber, int milliseconds) { }
    public void HoldCoil(int hwNumber) { }
    public void DisableCoil(int hwNumber) { }

    public void ConfigureFlipperRule(int switchHw, int mainCoilHw, int pulseMs, float holdPower = 0.25f) =>
        ConfiguredFlipperRules.Add(new FlipperRuleCall(switchHw, mainCoilHw, pulseMs, holdPower));

    public void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs) { }

    public void RemoveHardwareRule(int switchHw) => RemovedRules.Add(switchHw);

    public void SetLedColor(int hwAddress, byte r, byte g, byte b) { }
    public void SetLedColors(int startAddress, (byte r, byte g, byte b)[] colors) { }
}
