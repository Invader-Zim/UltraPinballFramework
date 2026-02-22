namespace UltraPinball.Core.Game;

public class HighScoreMode : Mode
{
    private readonly IHighScoreRepository _repository;
    private List<HighScoreEntry> _entries = [];

    public int MaxEntries { get; init; } = 10;
    public IReadOnlyList<HighScoreEntry> Entries => _entries;
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    public HighScoreMode(IHighScoreRepository repository) : base(priority: 1)
        => _repository = repository;

    public bool IsHighScore(long score) =>
        _entries.Count < MaxEntries || score > _entries[^1].Score;

    public override void ModeStarted()
    {
        _entries = _repository.Load().ToList();
        Game.GameEnded += OnGameEnded;
    }

    public override void ModeStopped() => Game.GameEnded -= OnGameEnded;

    private void OnGameEnded()
    {
        var changed = false;
        foreach (var player in Game.Players)
        {
            if (!IsHighScore(player.Score)) continue;
            _entries.Add(new HighScoreEntry(player.Name, player.Score, DateTime.UtcNow));
            changed = true;
        }
        if (!changed) return;

        _entries = _entries
            .OrderByDescending(e => e.Score)
            .Take(MaxEntries)
            .ToList();
        _repository.Save(_entries);
        Game.Media?.Post(MediaEvents.HighScoreUpdated,
            new { entries = _entries.Select(e => new { e.Name, e.Score, e.Date }) });
    }
}
