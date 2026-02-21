using Microsoft.Extensions.Logging;
using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// Built-in framework mode that manages the full ball lifecycle: launching balls from
/// the trough to the shooter lane, detecting drains, and supporting multiball.
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
/// While the window is open, a drain of the final ball re-ejects it instead of ending the ball.
/// Ball save does not affect individual multiball drains — only the very last ball.
/// </para>
/// <para>
/// <b>Multiball:</b> Call <see cref="AddBall"/> to eject an additional ball. The count is
/// confirmed in play once the shooter-lane switch goes inactive. <see cref="MultiBallStarted"/>
/// fires when the second ball enters play; <see cref="MultiBallEnded"/> fires when a drain
/// reduces the count back to one.
/// </para>
/// <para>
/// Override <see cref="OnBallDrained"/> to add pre-drain logic. It is called only when the
/// last ball drains. Call <see cref="GameController.EndBall"/> when ready.
/// </para>
/// </remarks>
public class TroughMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    private readonly string[] _troughSwitchNames;
    private readonly string _ejectCoilName;
    private readonly string _shooterLaneSwitchName;

    private int _ballsInPlay;
    private int _launchingCount;

    // ── Ball save ─────────────────────────────────────────────────────────────

    private const string BallSaveDelayName = "ball_save";

    /// <summary>
    /// True while the ball save window is open. A drain of the final ball during this
    /// window re-ejects it instead of ending the ball.
    /// </summary>
    public bool BallSaveActive => IsDelayed(BallSaveDelayName);

    /// <summary>
    /// When greater than zero, ball save automatically opens for this many seconds
    /// each time <see cref="GameController.BallStarting"/> fires.
    /// Set to 0 (the default) to disable auto-start.
    /// </summary>
    public float AutoBallSaveSeconds { get; set; } = 0f;

    /// <summary>Fired each time the final ball's drain is intercepted and the ball is re-ejected.</summary>
    public event Action? BallSaved;

    /// <summary>
    /// Fired when the last ball drains and ball save is not active.
    /// If subscribed, the subscriber is responsible for eventually calling
    /// <see cref="GameController.EndBall"/>. If no subscriber is attached,
    /// <see cref="GameController.EndBall"/> is called immediately.
    /// </summary>
    public event Action? BallDrained;

    /// <summary>
    /// Fired when the ball save window closes naturally after its duration elapses.
    /// Not raised when <see cref="StopBallSave"/> is called explicitly.
    /// </summary>
    public event Action? BallSaveExpired;

    /// <summary>
    /// Opens the ball save window for the specified duration.
    /// If already open, restarts the timer.
    /// </summary>
    public void StartBallSave(float seconds) =>
        Delay(seconds, OnBallSaveTimerExpired, BallSaveDelayName);

    /// <summary>Closes the ball save window immediately. <see cref="BallSaveExpired"/> is not raised.</summary>
    public void StopBallSave() => CancelDelay(BallSaveDelayName);

    // ── Multiball ─────────────────────────────────────────────────────────────

    /// <summary>Number of balls currently in play on the playfield.</summary>
    public int BallsInPlay => _ballsInPlay;

    /// <summary>True when more than one ball is simultaneously in play.</summary>
    public bool IsMultiBallActive => _ballsInPlay > 1;

    /// <summary>
    /// Fired when a second ball enters the playfield and multiball becomes active.
    /// </summary>
    public event Action? MultiBallStarted;

    /// <summary>
    /// Fired when a drain during multiball reduces the count back to one ball in play.
    /// Single-ball play resumes; the ball has not yet ended.
    /// </summary>
    public event Action? MultiBallEnded;

    /// <summary>
    /// Ejects an additional ball from the trough into the shooter lane.
    /// <see cref="BallsInPlay"/> increments and <see cref="MultiBallStarted"/> fires once
    /// the shooter-lane switch goes inactive (ball confirmed on the playfield).
    /// </summary>
    public void AddBall()
    {
        _launchingCount++;
        Log.LogInformation("TroughMode: ejecting additional ball for multiball.");
        Game.Coils[_ejectCoilName].Pulse();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a new TroughMode.
    /// </summary>
    /// <param name="troughSwitchNames">
    /// Ordered list of trough switch names. Each switch going active while a ball is in
    /// play signals a drain.
    /// </param>
    /// <param name="ejectCoilName">Coil that kicks a ball from the trough to the shooter lane.</param>
    /// <param name="shooterLaneSwitchName">
    /// Shooter-lane switch. Going inactive (ball left) confirms the ball is on the playfield.
    /// </param>
    /// <param name="priority">Mode priority. Defaults to 10.</param>
    public TroughMode(
        IReadOnlyList<string> troughSwitchNames,
        string ejectCoilName = "TroughEject",
        string shooterLaneSwitchName = "ShooterLane",
        int priority = 10) : base(priority)
    {
        _troughSwitchNames      = [.. troughSwitchNames];
        _ejectCoilName          = ejectCoilName;
        _shooterLaneSwitchName  = shooterLaneSwitchName;
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
        _ballsInPlay    = 0;
        _launchingCount = 1;
        Log.LogDebug("TroughMode: ejecting ball {Ball}.", ball);
        Game.Coils[_ejectCoilName].Pulse();
        if (AutoBallSaveSeconds > 0)
            StartBallSave(AutoBallSaveSeconds);
    }

    private SwitchHandlerResult OnShooterLaneInactive(Switch sw)
    {
        if (_launchingCount > 0 && Game.IsGameInProgress)
        {
            _launchingCount--;
            _ballsInPlay++;
            Log.LogDebug("TroughMode: ball confirmed in play ({N} total).", _ballsInPlay);

            if (_ballsInPlay == 2)
            {
                Log.LogInformation("Multiball started — {N} balls in play.", _ballsInPlay);
                MultiBallStarted?.Invoke();
                Game.Media?.Post(MediaEvents.MultiBallStarted, new { balls_in_play = _ballsInPlay });
            }
        }
        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnTroughActive(Switch sw)
    {
        if (!Game.IsGameInProgress || _ballsInPlay == 0)
            return SwitchHandlerResult.Continue;

        _ballsInPlay--;
        Log.LogInformation("[DRAIN] {Switch} → {N} ball(s) remaining. Score: {Score:N0}",
            sw.Name, _ballsInPlay, Game.CurrentPlayer?.Score);

        if (_ballsInPlay > 0)
        {
            if (_ballsInPlay == 1)
            {
                Log.LogInformation("Multiball ended — back to single ball.");
                MultiBallEnded?.Invoke();
                Game.Media?.Post(MediaEvents.MultiBallEnded);
            }
            return SwitchHandlerResult.Stop;
        }

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
    /// Called when the last ball drains. Checks ball save first; if active,
    /// re-ejects the ball and fires <see cref="BallSaved"/>. Otherwise calls
    /// <see cref="GameController.EndBall"/>.
    ///
    /// Override to add pre-drain logic. Call <see cref="GameController.EndBall"/>
    /// when ready to end the ball.
    /// </summary>
    protected virtual void OnBallDrained(Switch sw)
    {
        if (BallSaveActive)
        {
            Log.LogInformation("Ball saved!");
            _launchingCount++;
            Game.Coils[_ejectCoilName].Pulse();
            BallSaved?.Invoke();
            return;
        }

        if (BallDrained != null)
            BallDrained.Invoke();
        else
            Game.EndBall();
    }
}
