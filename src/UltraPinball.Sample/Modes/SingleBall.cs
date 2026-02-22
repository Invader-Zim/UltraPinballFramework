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
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.Ball;

    private readonly DoubleScoring _doubleScoring = new();
    private readonly BonusMode?   _bonus;

    public SingleBall(BonusMode? bonus = null) : base(priority: 20) { _bonus = bonus; }

    public override void ModeStarted()
    {
        AddSwitchHandler("LeftOutlane",  SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("LeftInlane",   SwitchActivation.Active, OnLeftInlane);
        AddSwitchHandler("LeftSling",    SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightOutlane", SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightInlane",  SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightSling",   SwitchActivation.Active, OnScoringSwitch);
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

        AddChildMode(_doubleScoring);

        return SwitchHandlerResult.Continue;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AwardPoints(int points, string source)
    {
        if (Game.CurrentPlayer == null) return;

        Game.CurrentPlayer.Score += points;
        Game.CurrentPlayer.IncrementBallState("hits");
        _bonus?.AddBonus(points);

        var hits = Game.CurrentPlayer.GetBallState<long>("hits");

        Log.LogInformation("[SINGLE BALL] +{Points} from {Source} → {Total:N0} (hit #{Hits} this ball)",
            points, source, Game.CurrentPlayer.Score, hits);

        Game.Media?.Post(SampleMediaEvents.PointsScored, new
        {
            player = Game.CurrentPlayer.Name,
            points,
            source,
            total  = Game.CurrentPlayer.Score,
            hits,
        });
    }
}
