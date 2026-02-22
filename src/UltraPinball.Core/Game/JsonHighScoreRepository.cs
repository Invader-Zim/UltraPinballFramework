using System.Text.Json;

namespace UltraPinball.Core.Game;

public class JsonHighScoreRepository : IHighScoreRepository
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public JsonHighScoreRepository(string filePath = "high_scores.json")
        => _filePath = filePath;

    public IReadOnlyList<HighScoreEntry> Load()
    {
        if (!File.Exists(_filePath)) return [];
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<HighScoreEntry>>(json, _options) ?? [];
    }

    public void Save(IReadOnlyList<HighScoreEntry> entries)
        => File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, _options));
}
