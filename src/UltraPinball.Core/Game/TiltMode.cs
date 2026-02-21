using Microsoft.Extensions.Logging;
using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// Stores the parameters needed to remove and restore a flipper's hardware rule.
/// Pass a list of these to <see cref="TiltMode"/> so tilt can disable flippers
/// and cleanly re-enable them at ball end.
/// </summary>
public record FlipperConfig(
    string SwitchName,
    string CoilName,
    int PulseMs,
    float HoldPower = 0.25f);

/// <summary>
/// Handles tilt-bob and slam-tilt behaviour.
///
/// <para>
/// The tilt-bob switch bounces — a single bump can close the switch several times.
/// A configurable cooldown window suppresses duplicates. After
/// <see cref="WarningCount"/> exceeds <c>warningsAllowed</c>, the ball is tilted:
/// flipper hardware rules are removed, <see cref="Tilted"/> fires so game code can
/// cancel ball save and disable scoring, and the ball is allowed to drain normally
/// (no bonus awarded).
/// </para>
/// <para>
/// An optional slam-tilt switch fires <see cref="SlamTilted"/> and immediately ends
/// the entire game for all players.
/// </para>
/// </summary>
public class TiltMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.Ball;

    private readonly string _tiltSwitchName;
    private readonly string? _slamTiltSwitchName;
    private readonly int _warningsAllowed;
    private readonly IReadOnlyList<FlipperConfig> _flippers;
    private readonly float _cooldownSeconds;

    private const string TiltCooldownDelay = "tilt_cooldown";

    private int _warningCount;
    private bool _tilted;

    /// <summary>True once the ball is tilted; resets to false each new ball.</summary>
    public bool IsTilted => _tilted;

    /// <summary>Number of tilt warnings issued this ball.</summary>
    public int WarningCount => _warningCount;

    /// <summary>Fired each time a tilt warning is issued. Payload: 1-based warning count.</summary>
    public event Action<int>? TiltWarning;

    /// <summary>
    /// Fired when the ball tilts. Subscribe here to cancel ball save, disable scoring, etc.
    /// TiltMode does not hold a reference to TroughMode — this decoupling is intentional.
    /// </summary>
    public event Action? Tilted;

    /// <summary>Fired when the slam-tilt switch activates, just before the game ends.</summary>
    public event Action? SlamTilted;

    /// <summary>
    /// Initialises TiltMode.
    /// </summary>
    /// <param name="tiltSwitchName">Name of the tilt-bob switch as declared in <see cref="MachineConfig"/>.</param>
    /// <param name="slamTiltSwitchName">
    /// Optional slam-tilt switch name. When it activates the game ends immediately.
    /// </param>
    /// <param name="warningsAllowed">Number of warnings before tilt occurs. Default 2.</param>
    /// <param name="flippers">
    /// Flipper configurations. When the ball tilts, the hardware rule for each flipper switch
    /// is removed. Rules are restored automatically when the ball ends.
    /// </param>
    /// <param name="priority">Mode priority. Defaults to 90 (above most game modes).</param>
    /// <param name="cooldownSeconds">
    /// How long to suppress additional tilt-bob activations after each hit (bob switches bounce).
    /// Defaults to 0.5 s; pass a shorter value in tests to avoid real-time delays.
    /// </param>
    public TiltMode(
        string tiltSwitchName,
        string? slamTiltSwitchName = null,
        int warningsAllowed = 2,
        IReadOnlyList<FlipperConfig>? flippers = null,
        int priority = 90,
        float cooldownSeconds = 0.5f) : base(priority)
    {
        _tiltSwitchName    = tiltSwitchName;
        _slamTiltSwitchName = slamTiltSwitchName;
        _warningsAllowed   = warningsAllowed;
        _flippers          = flippers ?? [];
        _cooldownSeconds   = cooldownSeconds;
    }

    /// <inheritdoc />
    public override void ModeStarted()
    {
        _warningCount = 0;
        _tilted       = false;

        AddSwitchHandler(_tiltSwitchName, SwitchActivation.Active, OnTiltSwitch);
        if (_slamTiltSwitchName != null)
            AddSwitchHandler(_slamTiltSwitchName, SwitchActivation.Active, OnSlamTiltSwitch);
    }

    /// <inheritdoc />
    public override void ModeStopped()
    {
        if (!_tilted) return;

        // Restore flipper hardware rules so the next ball can flip normally.
        foreach (var f in _flippers)
        {
            var sw   = Game.Switches[f.SwitchName];
            var coil = Game.Coils[f.CoilName];
            Game.Hardware.ConfigureFlipperRule(sw.HwNumber, coil.HwNumber, f.PulseMs, f.HoldPower);
        }
    }

    // ── Switch handlers ────────────────────────────────────────────────────────

    private SwitchHandlerResult OnTiltSwitch(Switch sw)
    {
        if (_tilted || IsDelayed(TiltCooldownDelay))
            return SwitchHandlerResult.Continue;

        _warningCount++;
        Delay(_cooldownSeconds, () => { }, TiltCooldownDelay);

        if (_warningCount <= _warningsAllowed)
        {
            TiltWarning?.Invoke(_warningCount);
            Game.Media?.Post(MediaEvents.TiltWarning, new { warning = _warningCount, allowed = _warningsAllowed });
        }
        else
        {
            DoTilt();
        }

        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnSlamTiltSwitch(Switch sw)
    {
        if (_tilted)
            return SwitchHandlerResult.Continue;

        DoTilt();
        SlamTilted?.Invoke();
        Game.Media?.Post(MediaEvents.SlamTilted);
        Game.EndGame();
        return SwitchHandlerResult.Stop;
    }

    private void DoTilt()
    {
        _tilted = true;
        foreach (var f in _flippers)
            Game.Hardware.RemoveHardwareRule(Game.Switches[f.SwitchName].HwNumber);
        Tilted?.Invoke();
        Game.Media?.Post(MediaEvents.Tilted);
        Log.LogInformation("TILT!");
    }
}
