namespace UltraPinball.Core.Game;

/// <summary>One entry on the high-score leaderboard.</summary>
public record HighScoreEntry(string Name, long Score, DateTime Date);
