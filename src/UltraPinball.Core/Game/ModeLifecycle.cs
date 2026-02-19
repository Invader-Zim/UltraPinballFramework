namespace UltraPinball.Core.Game;

/// <summary>
/// Controls when a mode registered via <see cref="GameController.RegisterMode"/> is
/// automatically added to and removed from the <see cref="ModeQueue"/>.
/// </summary>
public enum ModeLifecycle
{
    /// <summary>
    /// Added once at startup and never removed. Use for machine-level modes
    /// (attract, trough, machine monitor) that must always be active.
    /// </summary>
    System,

    /// <summary>
    /// Added when a game starts and removed when it ends. Use for modes that span
    /// all balls of a game but should not run during attract.
    /// </summary>
    Game,

    /// <summary>
    /// Added at the start of each ball and removed at the end. Use for per-ball
    /// features (auto-launch, ball-specific scoring modes, etc.).
    /// </summary>
    Ball,

    /// <summary>
    /// Not managed automatically. The programmer calls <see cref="ModeQueue.Add"/>
    /// and <see cref="ModeQueue.Remove"/> directly.
    /// </summary>
    Manual
}
