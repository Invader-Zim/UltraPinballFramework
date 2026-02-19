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
/// Override <see cref="OnBallDrained"/> to add ball-save logic, multiball drain tracking,
/// or any other behaviour before calling <see cref="GameController.EndBall"/>.
/// </remarks>
public class TroughMode : Mode
{
    private readonly string[] _troughSwitchNames;
    private readonly string _ejectCoilName;
    private readonly string _shooterLaneSwitchName;

    private bool _ballInPlay;
    private bool _launching;

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
        _launching = true;
        Log.LogDebug("TroughMode: ejecting ball {Ball}.", ball);
        Game.Coils[_ejectCoilName].Pulse();
    }

    private SwitchHandlerResult OnShooterLaneInactive(Switch sw)
    {
        if (_launching && Game.IsGameInProgress)
        {
            _launching = false;
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

    // ── Overridable drain callback ─────────────────────────────────────────────

    /// <summary>
    /// Called when a ball drain is confirmed. Default implementation calls
    /// <see cref="GameController.EndBall"/> immediately.
    ///
    /// Override to implement ball save, multiball drain tracking, or other
    /// pre-drain logic. Call <see cref="GameController.EndBall"/> when ready.
    /// </summary>
    /// <param name="sw">The trough switch that triggered the drain.</param>
    protected virtual void OnBallDrained(Switch sw) => Game.EndBall();
}
