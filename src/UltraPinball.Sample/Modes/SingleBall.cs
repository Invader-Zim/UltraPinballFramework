using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Core ball mode — awards 100 pts per scoring switch and owns the DoubleScoring sub-mode.
/// Registered with <see cref="ModeLifecycle.Ball"/> so it is active for exactly one ball.
/// </summary>
public class SingleBall : Mode
{
    private readonly DoubleScoring _doubleScoring = new();

    public SingleBall() : base(priority: 20) { }

    public override void ModeStarted()
    {
        AddSwitchHandler("LeftOutlane",  SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("LeftInlane",   SwitchActivation.Active, OnLeftInlane);
        AddSwitchHandler("LeftSling",    SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightOutlane", SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightInlane",  SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightSling",   SwitchActivation.Active, OnScoringSwitch);
    }

    public override void ModeStopped()
    {
        // Clean up DoubleScoring if the ball ends while it is still running.
        if (Game.Modes.Contains(_doubleScoring))
            Game.Modes.Remove(_doubleScoring);
    }

    // ── Switch handlers ───────────────────────────────────────────────────────

    private SwitchHandlerResult OnScoringSwitch(Switch sw)
    {
        AwardPoints(100, sw.Name);
        return SwitchHandlerResult.Continue;
    }

    private SwitchHandlerResult OnLeftInlane(Switch sw)
    {
        AwardPoints(100, sw.Name);

        // Start DoubleScoring on first hit; extend the timer on subsequent hits.
        if (Game.Modes.Contains(_doubleScoring))
            _doubleScoring.Extend();
        else
            Game.Modes.Add(_doubleScoring);

        return SwitchHandlerResult.Continue;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AwardPoints(int points, string source)
    {
        if (Game.CurrentPlayer == null) return;

        Game.CurrentPlayer.Score += points;

        Log.LogInformation("[SINGLE BALL] +{Points} from {Source} → {Total:N0}",
            points, source, Game.CurrentPlayer.Score);

        Game.Media?.Post("points_scored", new
        {
            player = Game.CurrentPlayer.Name,
            points,
            source,
            total  = Game.CurrentPlayer.Score,
        });
    }
}
