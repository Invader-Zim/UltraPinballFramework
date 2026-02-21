using Microsoft.Extensions.Logging;
using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// Built-in framework mode that manages the full ball lifecycle: launching balls from
/// the trough to the shooter lane and detecting drains.
///
/// <para>
/// Register as <see cref="ModeLifecycle.System"/> so it is always active. The mode
/// subscribes to <see cref="GameController.BallStarting"/> to automatically launch each
/// ball via the eject coil, then watches the shooter-lane switch to detect when the ball
/// has left the plunger lane and is in play.
/// </para>
/// <para>
/// Trough switches should be declared as <see cref="SwitchType.NormallyClosed"/> in
/// <see cref="MachineConfig"/>. Active (beam broken) means a ball is present.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Ball save:</b> Set <see cref="AutoBallSaveSeconds"/> to automatically open a save
/// window on each ball launch, or call <see cref="StartBallSave"/> manually from game code.
/// While the window is open, any drain re-ejects the ball instead of ending the ball.
/// </para>
/// <para>
/// Override <see cref="OnBallDrained"/> to add multiball drain tracking or other
/// pre-drain logic. Call <see cref="GameController.EndBall"/> when ready.
/// </para>
/// </remarks>
public class TroughMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    private readonly string[] _troughSwitchNames;
    private readonly string _ejectCoilName;
    private readonly string _shooterLaneSwitchName;

    private bool _ballInPlay;
    private bool _launching;

    // ── Ball save ─────────────────────────────────────────────────────────────

    private const string BallSaveDelayName = "ball_save";

    /// <summary>
    /// True while the ball save window is open. Any drain during this window
    /// re-ejects the ball instead of ending it.
    /// </summary>
    public bool BallSaveActive => IsDelayed(BallSaveDelayName);

    /// <summary>
    /// When greater than zero, ball save automatically opens for this many seconds
    /// each time <see cref="GameController.BallStarting"/> fires.
    /// Set to 0 (the default) to disable auto-start.
    /// </summary>
    public float AutoBallSaveSeconds { get; set; } = 0f;

    /// <summary>Fired each time a drain is intercepted and the ball is re-ejected.</summary>
    public event Action? BallSaved;

    /// <summary>
    /// Fired when the ball save window closes naturally after its duration elapses.
    /// Not raised when <see cref="StopBallSave"/> is called explicitly.
    /// </summary>
    public event Action? BallSaveExpired;

    /// <summary>
    /// Opens the ball save window for the specified duration.
    /// If already open, restarts the timer.
    /// </summary>
    /// <param name="seconds">How long the save window should remain open.</param>
    public void StartBallSave(float seconds) =>
        Delay(seconds, OnBallSaveTimerExpired, BallSaveDelayName);

    /// <summary>
    /// Closes the ball save window immediately.
    /// <see cref="BallSaveExpired"/> is not raised.
    /// </summary>
    public void StopBallSave() => CancelDelay(BallSaveDelayName);

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a new TroughMode.
    /// </summary>
    /// <param name="troughSwitchNames">
    /// Ordered list of trough switch names as declared in <see cref="MachineConfig"/>.
    /// Each switch going active (beam broken) while a ball is in play signals a drain.
    /// </param>
    /// <param name="ejectCoilName">Name of the coil that kicks a ball from the trough to the shooter lane.</param>
    /// <param name="shooterLaneSwitchName">
    /// Name of the shooter-lane switch. Going inactive (ball left) marks the ball as in play.
    /// </param>
    /// <param name="priority">Mode priority. Defaults to 10.</param>
    public TroughMode(
        IReadOnlyList<string> troughSwitchNames,
        string ejectCoilName = "TroughEject",
        string shooterLaneSwitchName = "ShooterLane",
        int priority = 10) : base(priority)
    {
        _troughSwitchNames = [.. troughSwitchNames];
        _ejectCoilName = ejectCoilName;
        _shooterLaneSwitchName = shooterLaneSwitchName;
    }

    /// <inheritdoc />
    public override void ModeStarted()
    {
        Game.BallStarting += OnBallStarting;
        AddSwitchHandler(_shooterLaneSwitchName, SwitchActivation.Inactive, OnShooterLaneInactive);
        foreach (var name in _troughSwitchNames)
            AddSwitchHandler(name, SwitchActivation.Active, OnTroughActive);
    }

    /// <inheritdoc />
    public override void ModeStopped()
    {
        Game.BallStarting -= OnBallStarting;
    }

    // ── Private handlers ──────────────────────────────────────────────────────

    private void OnBallStarting(int ball)
    {
        _ballInPlay = false;
        _launching  = true;
        Log.LogDebug("TroughMode: ejecting ball {Ball}.", ball);
        Game.Coils[_ejectCoilName].Pulse();
        if (AutoBallSaveSeconds > 0)
            StartBallSave(AutoBallSaveSeconds);
    }

    private SwitchHandlerResult OnShooterLaneInactive(Switch sw)
    {
        if (_launching && Game.IsGameInProgress)
        {
            _launching  = false;
            _ballInPlay = true;
            Log.LogDebug("TroughMode: ball in play.");
        }
        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnTroughActive(Switch sw)
    {
        if (!Game.IsGameInProgress || !_ballInPlay)
            return SwitchHandlerResult.Continue;

        _ballInPlay = false;
        Log.LogInformation("[DRAIN] Ball {Ball} drained into {Switch}. Score: {Score:N0}",
            Game.Ball, sw.Name, Game.CurrentPlayer?.Score);
        OnBallDrained(sw);
        return SwitchHandlerResult.Stop;
    }

    private void OnBallSaveTimerExpired()
    {
        Log.LogDebug("TroughMode: ball save expired.");
        BallSaveExpired?.Invoke();
    }

    // ── Overridable drain callback ─────────────────────────────────────────────

    /// <summary>
    /// Called when a ball drain is confirmed. Checks ball save first; if active,
    /// re-ejects the ball and fires <see cref="BallSaved"/>. Otherwise calls
    /// <see cref="GameController.EndBall"/>.
    ///
    /// Override to add multiball drain tracking or other pre-drain logic.
    /// Call <see cref="GameController.EndBall"/> when ready to end the ball.
    /// </summary>
    /// <param name="sw">The trough switch that triggered the drain.</param>
    protected virtual void OnBallDrained(Switch sw)
    {
        if (BallSaveActive)
        {
            Log.LogInformation("Ball saved!");
            _launching = true;
            Game.Coils[_ejectCoilName].Pulse();
            BallSaved?.Invoke();
            return;
        }
        Game.EndBall();
    }
}
