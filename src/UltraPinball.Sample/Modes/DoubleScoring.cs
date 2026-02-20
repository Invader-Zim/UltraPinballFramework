using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Bonus mode that awards an extra 100 pts on every scoring switch while active.
/// <para>
/// Activated by <see cref="SingleBall"/> when LeftInlane is hit. Expires after 10 s;
/// hitting LeftInlane again while active resets the timer to a fresh 10 s.
/// </para>
/// </summary>
public class DoubleScoring : Mode
{
    private const float DurationSeconds = 10f;
    private const string TimerName      = "double_scoring_timer";

    // Priority 19 — lower than SingleBall (20) so base points are posted first.
    public DoubleScoring() : base(priority: 19) { }

    public override void ModeStarted()
    {
        AddSwitchHandler("LeftOutlane",  SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("LeftInlane",   SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("LeftSling",    SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightOutlane", SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightInlane",  SwitchActivation.Active, OnScoringSwitch);
        AddSwitchHandler("RightSling",   SwitchActivation.Active, OnScoringSwitch);

        ScheduleTimer();

        Log.LogInformation("[DOUBLE SCORING] Started — {Duration}s", DurationSeconds);
        Game.Media?.Post("double_scoring_started", new { duration_seconds = DurationSeconds });
    }

    public override void ModeStopped()
    {
        Log.LogInformation("[DOUBLE SCORING] Ended");
        Game.Media?.Post("double_scoring_ended", null);
    }

    /// <summary>Resets the expiry timer back to full duration.</summary>
    public void Extend()
    {
        ScheduleTimer();
        Log.LogInformation("[DOUBLE SCORING] Extended — {Duration}s", DurationSeconds);
        Game.Media?.Post("double_scoring_extended", new { duration_seconds = DurationSeconds });
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void ScheduleTimer()
    {
        // Explicit cancel ensures a previous pending delay is replaced even if
        // Delay() with the same name does not automatically remove the old entry.
        CancelDelay(TimerName);
        Delay(DurationSeconds, () => Game.Modes.Remove(this), name: TimerName);
    }

    private SwitchHandlerResult OnScoringSwitch(Switch sw)
    {
        if (Game.CurrentPlayer == null) return SwitchHandlerResult.Continue;

        Game.CurrentPlayer.Score += 100;

        Log.LogInformation("[DOUBLE SCORING] +100 bonus from {Source} → {Total:N0}",
            sw.Name, Game.CurrentPlayer.Score);

        Game.Media?.Post("points_scored", new
        {
            player = Game.CurrentPlayer.Name,
            points = 100,
            source = sw.Name,
            bonus  = "double_scoring",
            total  = Game.CurrentPlayer.Score,
        });

        return SwitchHandlerResult.Continue;
    }
}
