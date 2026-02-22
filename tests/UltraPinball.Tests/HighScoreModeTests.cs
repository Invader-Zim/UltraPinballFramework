using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class HighScoreModeTests
{
    // ── In-memory repository ──────────────────────────────────────────────────

    private class InMemoryHighScoreRepository : IHighScoreRepository
    {
        private List<HighScoreEntry> _entries = [];
        public IReadOnlyList<HighScoreEntry> Load() => _entries;
        public void Save(IReadOnlyList<HighScoreEntry> entries) => _entries = entries.ToList();
    }

    // ── Build helper ──────────────────────────────────────────────────────────

    private static (GameController game, HighScoreMode mode, InMemoryHighScoreRepository repo) Build(
        InMemoryHighScoreRepository? repo = null)
    {
        repo ??= new InMemoryHighScoreRepository();
        var machine = new EmptyMachine();
        machine.Initialize(new NullPlatform());
        var game = new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
        var mode = new HighScoreMode(repo);
        game.RegisterMode(mode);   // System lifecycle → ModeStarted() fires immediately
        return (game, mode, repo);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void GameEnded_QualifyingScore_Saved()
    {
        var (game, mode, _) = Build();
        game.StartGame();
        game.CurrentPlayer!.Score = 5_000;

        game.EndGame();

        Assert.Single(mode.Entries);
        Assert.Equal(5_000, mode.Entries[0].Score);
    }

    [Fact]
    public void GameEnded_LowScore_NotSaved_WhenTableFull()
    {
        var repo = new InMemoryHighScoreRepository();
        // Pre-fill with MaxEntries=10 scores of 1000 each
        var existing = Enumerable.Range(1, 10)
            .Select(i => new HighScoreEntry($"Player {i}", 1_000, DateTime.UtcNow))
            .ToList();
        repo.Save(existing);

        var (game, mode, _) = Build(repo);
        game.StartGame();
        game.CurrentPlayer!.Score = 1;   // below all existing scores

        game.EndGame();

        Assert.Equal(10, mode.Entries.Count);
        Assert.DoesNotContain(mode.Entries, e => e.Score == 1);
    }

    [Fact]
    public void Entries_OrderedHighestFirst()
    {
        var repo = new InMemoryHighScoreRepository();
        var (game, mode, _) = Build(repo);

        // Game 1 — score 500
        game.StartGame();
        game.CurrentPlayer!.Score = 500;
        game.EndGame();

        // Game 2 — score 200
        game.StartGame();
        game.CurrentPlayer!.Score = 200;
        game.EndGame();

        Assert.Equal(2, mode.Entries.Count);
        Assert.Equal(500, mode.Entries[0].Score);
        Assert.Equal(200, mode.Entries[1].Score);
    }

    [Fact]
    public void IsHighScore_TrueWhenTableNotFull()
    {
        var (_, mode, _) = Build();

        // Board is empty — any score qualifies
        Assert.True(mode.IsHighScore(0));
    }

    [Fact]
    public void IsHighScore_FalseWhenBelowLowest()
    {
        var repo = new InMemoryHighScoreRepository();
        repo.Save([
            new HighScoreEntry("P1", 200, DateTime.UtcNow),
            new HighScoreEntry("P2", 100, DateTime.UtcNow),
        ]);

        var machine = new EmptyMachine();
        machine.Initialize(new NullPlatform());
        var game = new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
        var mode = new HighScoreMode(repo) { MaxEntries = 2 };
        game.RegisterMode(mode);

        Assert.False(mode.IsHighScore(50));
    }

    [Fact]
    public void MultiPlayer_AllQualifyingScoresSaved()
    {
        var (game, mode, _) = Build();
        game.StartGame();
        game.AddPlayer();   // now 2 players

        game.Players[0].Score = 1_000;
        game.Players[1].Score = 500;

        game.EndGame();

        Assert.Equal(2, mode.Entries.Count);
        Assert.Contains(mode.Entries, e => e.Score == 1_000);
        Assert.Contains(mode.Entries, e => e.Score == 500);
    }
}
