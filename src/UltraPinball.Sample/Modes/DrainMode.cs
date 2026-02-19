using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Always-on mode (priority 10). Tracks ball-in-play state and detects drains.
///
/// Ball lifecycle:
///   1. StartBall() fires TroughEject → ball rolls to ShooterLane (Active).
///   2. AutoLaunchMode fires AutoLaunch coil → ShooterLane goes Inactive.
///      → _ballInPlay = true
///   3. Ball on playfield.
///   4. Ball drains into trough → a trough opto goes Active (beam broken).
///      → EndBall()
///
/// Trough switches are normally-closed optos: Active = Open = ball present.
/// </summary>
public class DrainMode : Mode
{
    private bool _ballInPlay;

    public DrainMode() : base(priority: 10) { }

    public override void ModeStarted()
    {
        AddSwitchHandler("ShooterLane", SwitchActivation.Inactive, OnShooterLaneInactive);

        AddSwitchHandler("Trough0", SwitchActivation.Active, OnTroughActive);
        AddSwitchHandler("Trough1", SwitchActivation.Active, OnTroughActive);
        AddSwitchHandler("Trough2", SwitchActivation.Active, OnTroughActive);
        AddSwitchHandler("Trough3", SwitchActivation.Active, OnTroughActive);
        AddSwitchHandler("Trough4", SwitchActivation.Active, OnTroughActive);
    }

    private SwitchHandlerResult OnShooterLaneInactive(Switch sw)
    {
        if (Game.IsGameInProgress)
        {
            Log.LogDebug("DrainMode: ball in play");
            _ballInPlay = true;
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
        Game.EndBall();
        return SwitchHandlerResult.Stop;
    }
}
