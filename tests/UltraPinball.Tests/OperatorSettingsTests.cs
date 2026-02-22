using UltraPinball.Core.Game;
using UltraPinball.Simulator;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class OperatorSettingsTests
{
    // ── In-memory repository ──────────────────────────────────────────────────

    private class InMemoryOperatorSettingsRepository : IOperatorSettingsRepository
    {
        private OperatorSettings _settings = new();
        public OperatorSettings Load() => _settings;
        public void Save(OperatorSettings settings) => _settings = settings;
    }

    // ── Build helper ──────────────────────────────────────────────────────────

    private static GameController BuildGame()
    {
        var machine = new EmptyMachine();
        machine.Initialize(new NullPlatform());
        return new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_AreCorrect()
    {
        var settings = new OperatorSettings();

        Assert.Equal(3, settings.BallsPerGame);
        Assert.Equal(4, settings.MaxPlayers);
        Assert.Equal(2, settings.TiltWarnings);
        Assert.Equal(8f, settings.BallSaveSeconds);
    }

    [Fact]
    public void InMemoryRepository_SaveLoad_Roundtrips()
    {
        var repo = new InMemoryOperatorSettingsRepository();
        var saved = new OperatorSettings
        {
            BallsPerGame  = 5,
            MaxPlayers    = 2,
            TiltWarnings  = 0,
            BallSaveSeconds = 3.5f,
        };

        repo.Save(saved);
        var loaded = repo.Load();

        Assert.Equal(5,    loaded.BallsPerGame);
        Assert.Equal(2,    loaded.MaxPlayers);
        Assert.Equal(0,    loaded.TiltWarnings);
        Assert.Equal(3.5f, loaded.BallSaveSeconds);
    }

    [Fact]
    public void ApplySettings_Sets_BallsPerGame()
    {
        var game     = BuildGame();
        var settings = new OperatorSettings { BallsPerGame = 5 };

        game.ApplySettings(settings);

        Assert.Equal(5, game.BallsPerGame);
    }

    [Fact]
    public void ApplySettings_Sets_MaxPlayers()
    {
        var game     = BuildGame();
        var settings = new OperatorSettings { MaxPlayers = 2 };

        game.ApplySettings(settings);

        Assert.Equal(2, game.MaxPlayers);
    }

    [Fact]
    public void JsonRepository_ReturnsDefaults_WhenFileAbsent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"op_settings_{Guid.NewGuid():N}.json");
        var repo = new JsonOperatorSettingsRepository(path);

        var settings = repo.Load();

        Assert.Equal(new OperatorSettings(), settings);
    }
}
