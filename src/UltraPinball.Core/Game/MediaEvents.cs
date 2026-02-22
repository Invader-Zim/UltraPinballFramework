namespace UltraPinball.Core.Game;

/// <summary>
/// String constants for the core media event types posted by
/// <see cref="GameController"/> and the built-in framework modes.
///
/// <para>
/// Use these constants (rather than inline string literals) when calling
/// <see cref="IMediaEventSink.Post"/> so that media controllers and game code
/// share a single authoritative name for each event. Game projects may define
/// additional event types in their own static classes following the same pattern.
/// </para>
/// </summary>
public static class MediaEvents
{
    // ── Game lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// A new game has started and the first ball is about to be served.
    /// Payload: <c>{ player: string, balls_per_game: int }</c>.
    /// </summary>
    public const string GameStarted = "game_started";

    /// <summary>
    /// A ball is about to enter play (hardware may now eject a ball).
    /// Payload: <c>{ ball: int, player: string }</c>.
    /// </summary>
    public const string BallStarting = "ball_starting";

    /// <summary>
    /// A ball has drained and ball-lifecycle modes are being removed.
    /// Payload: <c>{ ball: int, player: string, score: long }</c>.
    /// </summary>
    public const string BallEnded = "ball_ended";

    /// <summary>
    /// The final ball of the final player has drained; the game is over.
    /// Payload: <c>{ scores: [{ name: string, score: long }] }</c>.
    /// </summary>
    public const string GameEnded = "game_ended";

    // ── Player management ──────────────────────────────────────────────────────

    /// <summary>
    /// A new player joined during the add-player window (Ball 1, before plunge).
    /// Payload: <c>{ player: string, total_players: int }</c>.
    /// </summary>
    public const string PlayerAdded = "player_added";

    // ── Attract ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attract loop is idle — waiting for the start button, or resuming after
    /// a game-over dwell completes. No payload.
    /// </summary>
    public const string AttractIdle = "attract_idle";

    // ── Tilt ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// A tilt warning was issued. The ball is still in play.
    /// Payload: <c>{ warning: int, allowed: int }</c>.
    /// </summary>
    public const string TiltWarning = "tilt_warning";

    /// <summary>
    /// The ball has tilted — flippers are disabled and no bonus will be awarded.
    /// No payload.
    /// </summary>
    public const string Tilted = "tilt";

    /// <summary>
    /// The slam-tilt switch fired — the entire game is ending immediately.
    /// No payload.
    /// </summary>
    public const string SlamTilted = "slam_tilt";

    // ── Multiball ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A second ball entered the playfield — multiball is now active.
    /// Payload: <c>{ balls_in_play: int }</c>.
    /// </summary>
    public const string MultiBallStarted = "multiball_started";

    /// <summary>
    /// A drain during multiball reduced the count back to one ball in play.
    /// Single-ball play resumes; the ball has not yet ended. No payload.
    /// </summary>
    public const string MultiBallEnded = "multiball_ended";

    // ── Bonus ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// End-of-ball bonus countdown started.
    /// Payload: <c>{ bonus: long, multiplier: int, total: long }</c>.
    /// </summary>
    public const string BonusStarted = "bonus_started";

    /// <summary>
    /// One countdown step completed — points have been awarded to the player.
    /// Payload: <c>{ awarded: long, remaining: long }</c>.
    /// </summary>
    public const string BonusStep = "bonus_step";

    /// <summary>
    /// Bonus countdown finished — all bonus points have been awarded.
    /// Payload: <c>{ awarded: long }</c>.
    /// </summary>
    public const string BonusCompleted = "bonus_completed";

    // ── Drop targets ──────────────────────────────────────────────────────────

    /// <summary>
    /// A single drop target was knocked down.
    /// Payload: <c>{ target: string }</c>.
    /// </summary>
    public const string DropTargetHit = "drop_target_hit";

    /// <summary>
    /// All targets in a bank are now down.
    /// Payload: <c>{ targets: int }</c>.
    /// </summary>
    public const string DropTargetBankComplete = "drop_target_bank_complete";

    /// <summary>
    /// The drop target bank reset coil was pulsed — all targets are returning to standing.
    /// No payload.
    /// </summary>
    public const string DropTargetBankReset = "drop_target_bank_reset";

    // ── Ball search ────────────────────────────────────────────────────────────

    /// <summary>
    /// No playfield switch has fired for the configured timeout — ball search has begun.
    /// No payload.
    /// </summary>
    public const string BallSearchStarted = "ball_search_started";

    /// <summary>
    /// A playfield switch fired during ball search — the ball has been found and the search stopped.
    /// No payload.
    /// </summary>
    public const string BallSearchStopped = "ball_search_stopped";
}
