namespace UltraPinball.Core.Game;

public interface IHighScoreRepository
{
    /// <summary>Loads the current leaderboard, ordered highest-score first.</summary>
    IReadOnlyList<HighScoreEntry> Load();

    /// <summary>Persists the leaderboard (already ordered and trimmed).</summary>
    void Save(IReadOnlyList<HighScoreEntry> entries);
}
