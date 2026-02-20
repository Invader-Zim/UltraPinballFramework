using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Ball-scoped mode (priority 50). When the ball arrives in the shooter lane,
/// waits briefly then fires the AutoLaunch coil to send it to the playfield.
/// </summary>
public class AutoLaunchMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.Ball;

    public AutoLaunchMode() : base(priority: 50) { }

    public override void ModeStarted()
    {
        AddSwitchHandler("ShooterLane", SwitchActivation.Active, OnShooterLaneActive);
    }

    private SwitchHandlerResult OnShooterLaneActive(Switch sw)
    {
        // 1-second delay lets the ball settle before launch.
        // Named so a re-trigger restarts the timer rather than stacking.
        Delay(1.0f, () =>
        {
            Log.LogInformation("[LAUNCH] Auto-launching ball.");
            Game.Coils["AutoLaunch"].Pulse();
        }, name: "auto_launch");

        return SwitchHandlerResult.Continue;
    }
}
