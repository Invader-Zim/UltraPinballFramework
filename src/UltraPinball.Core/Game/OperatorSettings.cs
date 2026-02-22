namespace UltraPinball.Core.Game;

/// <summary>Machine-wide operator settings, configurable without recompilation.</summary>
public record OperatorSettings
{
    /// <summary>Number of balls per game. Typical range 1–5. Default 3.</summary>
    public int BallsPerGame { get; init; } = 3;

    /// <summary>Maximum number of players per game. Typical range 1–4. Default 4.</summary>
    public int MaxPlayers { get; init; } = 4;

    /// <summary>Number of tilt warnings before the ball tilts. Default 2.</summary>
    public int TiltWarnings { get; init; } = 2;

    /// <summary>Automatic ball-save window in seconds on each ball launch. Default 8.</summary>
    public float BallSaveSeconds { get; init; } = 8f;
}
