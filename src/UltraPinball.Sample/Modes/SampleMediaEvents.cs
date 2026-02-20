namespace UltraPinball.Sample.Modes;

/// <summary>
/// String constants for the media event types posted by UltraPinball.Sample modes.
/// Follows the same pattern as <see cref="UltraPinball.Core.Game.MediaEvents"/>
/// for core events — extend with your own static class for game-specific events.
/// </summary>
public static class SampleMediaEvents
{
    // ── Scoring ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Points were awarded to the current player.
    /// Payload: <c>{ player: string, points: int, source: string, total: long }</c>.
    /// When posted by <c>DoubleScoring</c> an additional <c>bonus: string</c> field is included.
    /// </summary>
    public const string PointsScored = "points_scored";

    // ── Double Scoring ─────────────────────────────────────────────────────────

    /// <summary>
    /// Double-scoring mode became active.
    /// Payload: <c>{ duration_seconds: float }</c>.
    /// </summary>
    public const string DoubleScoringStarted = "double_scoring_started";

    /// <summary>
    /// Double-scoring mode expired.
    /// No payload.
    /// </summary>
    public const string DoubleScoringEnded = "double_scoring_ended";

    /// <summary>
    /// Double-scoring timer was reset because LeftInlane was hit while already active.
    /// Payload: <c>{ duration_seconds: float }</c>.
    /// </summary>
    public const string DoubleScoringExtended = "double_scoring_extended";
}
