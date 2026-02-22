using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// Manages a bank of drop targets — a group of individually-knockable targets
/// backed by a single reset coil that springs all targets back to standing.
///
/// <para>
/// Register the mode and subscribe to its events to award points, trigger
/// features, or schedule resets:
/// <code>
/// var bank = new DropTargetBank(["Left", "Center", "Right"], "DropReset",
///                               autoResetSeconds: 2f);
/// bank.AllTargetsDown += () => AwardPoints(5_000, "drop bank");
/// RegisterMode(bank);
/// </code>
/// </para>
/// </summary>
public class DropTargetBank : Mode
{
    private readonly string[] _targetNames;
    private readonly string _resetCoilName;
    private readonly float _autoResetSeconds;
    private readonly HashSet<string> _dropped = new();

    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.Ball;

    /// <summary><c>true</c> when every target in the bank is down.</summary>
    public bool IsComplete => _dropped.Count == _targetNames.Length;

    /// <summary>Total number of targets in the bank.</summary>
    public int TargetCount => _targetNames.Length;

    /// <summary>Number of targets currently dropped.</summary>
    public int DroppedCount => _dropped.Count;

    /// <summary>Switch names of the targets that are currently down.</summary>
    public IReadOnlySet<string> DroppedTargets => _dropped;

    /// <summary>Fires with the switch name each time a new target is knocked down.</summary>
    public event Action<string>? TargetHit;

    /// <summary>Fires when the last standing target is knocked down.</summary>
    public event Action? AllTargetsDown;

    /// <summary>Fires whenever the bank is reset (reset coil pulsed).</summary>
    public event Action? BankReset;

    /// <summary>
    /// Creates a drop target bank.
    /// </summary>
    /// <param name="targetSwitchNames">
    /// Symbolic switch names (as declared in <see cref="MachineConfig"/>) for each target.
    /// Switches should be normally-open; they close when the target is knocked down.
    /// </param>
    /// <param name="resetCoilName">
    /// Symbolic coil name for the reset solenoid that springs all targets back to standing.
    /// </param>
    /// <param name="autoResetSeconds">
    /// If greater than zero, the bank automatically resets this many seconds after all
    /// targets are down. Pass zero (the default) to require a manual call to <see cref="Reset"/>.
    /// </param>
    /// <param name="priority">Mode priority. Defaults to 30.</param>
    public DropTargetBank(string[] targetSwitchNames, string resetCoilName,
                          float autoResetSeconds = 0f, int priority = 30)
        : base(priority)
    {
        _targetNames      = targetSwitchNames;
        _resetCoilName    = resetCoilName;
        _autoResetSeconds = autoResetSeconds;
    }

    /// <inheritdoc />
    public override void ModeStarted()
    {
        _dropped.Clear();
        foreach (var name in _targetNames)
            AddSwitchHandler(name, SwitchActivation.Active, OnTargetHit);
    }

    /// <summary>
    /// Pulses the reset coil, raises all targets, and fires <see cref="BankReset"/>.
    /// Safe to call at any time — cancels a pending auto-reset delay if one is running.
    /// </summary>
    public void Reset()
    {
        _dropped.Clear();
        CancelDelay("drop_bank_auto_reset");
        Game.Coils[_resetCoilName].Pulse();
        BankReset?.Invoke();
        Game.Media?.Post(MediaEvents.DropTargetBankReset, new { });
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private SwitchHandlerResult OnTargetHit(Switch sw)
    {
        if (!_dropped.Add(sw.Name)) return SwitchHandlerResult.Continue; // bounce guard

        TargetHit?.Invoke(sw.Name);
        Game.Media?.Post(MediaEvents.DropTargetHit, new { target = sw.Name });

        if (IsComplete)
        {
            AllTargetsDown?.Invoke();
            Game.Media?.Post(MediaEvents.DropTargetBankComplete,
                             new { targets = _targetNames.Length });

            if (_autoResetSeconds > 0f)
                Delay(_autoResetSeconds, Reset, "drop_bank_auto_reset");
        }

        return SwitchHandlerResult.Continue;
    }
}
