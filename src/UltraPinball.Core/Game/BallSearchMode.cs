using Microsoft.Extensions.Logging;
using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// Monitors playfield switch activity and, when no switch fires for a configurable
/// period, begins pulsing a set of coils in round-robin to try to dislodge a stuck ball.
///
/// <para>
/// Switch tags drive all behaviour — no switch or coil names are hard-coded in this mode:
/// <list type="bullet">
///   <item><c>Playfield</c> switches reset the idle timer; during an active search they also stop it.</item>
///   <item><c>Eos</c> switches reset the idle timer (flipper held = player is active).</item>
///   <item>The <c>ShooterLane</c> switch suspends the timer while the ball is safely parked there
///         and resumes it when the ball leaves.</item>
/// </list>
/// </para>
/// <para>
/// Coils to pulse are supplied by the caller; pass the bumper and slingshot coils for the machine.
/// If no coils are supplied the timer logic still runs (useful for testing or minimal configs).
/// </para>
/// </summary>
public class BallSearchMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.Ball;

    private readonly IReadOnlyList<string> _searchCoilNames;
    private readonly float _timeoutSeconds;
    private readonly float _searchIntervalSeconds;

    private const string IdleTimerName   = "ball_search_idle";
    private const string SearchPulseName = "ball_search_pulse";

    private bool _searching;
    private int  _searchPhase;

    /// <summary>True while the mode is actively pulsing coils looking for the ball.</summary>
    public bool IsSearching => _searching;

    /// <summary>Fired when ball search begins (idle timeout elapsed).</summary>
    public event Action? BallSearchStarted;

    /// <summary>Fired when ball search stops because a playfield switch was hit.</summary>
    public event Action? BallSearchStopped;

    /// <summary>
    /// Creates a <see cref="BallSearchMode"/>.
    /// </summary>
    /// <param name="searchCoilNames">
    /// Coils to pulse during a search, in round-robin order (e.g. bumpers, slings).
    /// Pass <c>null</c> or an empty list to run timer-only mode without coil pulsing.
    /// </param>
    /// <param name="timeoutSeconds">Seconds of playfield inactivity before search begins. Default 15 s.</param>
    /// <param name="searchIntervalSeconds">Pause between each coil pulse during a search. Default 0.25 s.</param>
    /// <param name="priority">Mode priority. Defaults to 10 (low — runs beneath most game modes).</param>
    public BallSearchMode(
        IReadOnlyList<string>? searchCoilNames = null,
        float timeoutSeconds        = 15f,
        float searchIntervalSeconds = 0.25f,
        int   priority              = 10) : base(priority)
    {
        _searchCoilNames       = searchCoilNames ?? [];
        _timeoutSeconds        = timeoutSeconds;
        _searchIntervalSeconds = searchIntervalSeconds;
    }

    /// <inheritdoc />
    public override void ModeStarted()
    {
        _searching   = false;
        _searchPhase = 0;

        Switch? shooterLane = null;

        foreach (var sw in Game.Switches)
        {
            if (sw.Tags.HasFlag(SwitchTags.Playfield))
            {
                AddSwitchHandler(sw.Name, SwitchActivation.Active, OnPlayfieldSwitch);
            }
            else if (sw.Tags.HasFlag(SwitchTags.Eos))
            {
                AddSwitchHandler(sw.Name, SwitchActivation.Active, OnEosSwitch);
            }
            else if (sw.Tags.HasFlag(SwitchTags.ShooterLane))
            {
                shooterLane = sw;
                AddSwitchHandler(sw.Name, SwitchActivation.Active,   OnShooterLaneActive);
                AddSwitchHandler(sw.Name, SwitchActivation.Inactive, OnShooterLaneInactive);
            }
        }

        // If the ball is already in the shooter lane, don't start the idle timer yet.
        if (shooterLane == null || !shooterLane.IsActive)
            StartIdleTimer();
    }

    // ── Switch handlers ────────────────────────────────────────────────────────

    private SwitchHandlerResult OnPlayfieldSwitch(Switch sw)
    {
        if (_searching)
            StopSearch();
        else
            ResetIdleTimer();

        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnEosSwitch(Switch sw)
    {
        if (!_searching) ResetIdleTimer();
        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnShooterLaneActive(Switch sw)
    {
        SuspendIdleTimer();
        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnShooterLaneInactive(Switch sw)
    {
        if (!_searching) StartIdleTimer();
        return SwitchHandlerResult.Continue;
    }

    // ── Timer helpers ──────────────────────────────────────────────────────────

    private void StartIdleTimer()
    {
        CancelDelay(IdleTimerName);
        Delay(_timeoutSeconds, OnIdleTimeout, IdleTimerName);
    }

    private void ResetIdleTimer() => StartIdleTimer();

    private void SuspendIdleTimer() => CancelDelay(IdleTimerName);

    // ── Search logic ───────────────────────────────────────────────────────────

    private void OnIdleTimeout()
    {
        _searching   = true;
        _searchPhase = 0;

        Log.LogWarning("Ball search started after {Seconds}s of playfield inactivity.", _timeoutSeconds);
        BallSearchStarted?.Invoke();
        Game.Media?.Post(MediaEvents.BallSearchStarted);

        PulseNextCoil();
    }

    private void PulseNextCoil()
    {
        if (!_searching || _searchCoilNames.Count == 0) return;

        var coilName = _searchCoilNames[_searchPhase % _searchCoilNames.Count];
        Game.Coils[coilName].Pulse();
        Log.LogDebug("Ball search: pulsed {Coil} (phase {Phase}).", coilName, _searchPhase);
        _searchPhase++;

        Delay(_searchIntervalSeconds, PulseNextCoil, SearchPulseName);
    }

    private void StopSearch()
    {
        _searching = false;
        CancelDelay(SearchPulseName);

        Log.LogInformation("Ball search stopped — playfield activity detected.");
        BallSearchStopped?.Invoke();
        Game.Media?.Post(MediaEvents.BallSearchStopped);

        StartIdleTimer();
    }
}
