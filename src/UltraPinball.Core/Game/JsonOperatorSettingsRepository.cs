using System.Text.Json;

namespace UltraPinball.Core.Game;

public class JsonOperatorSettingsRepository : IOperatorSettingsRepository
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public JsonOperatorSettingsRepository(string filePath = "operator_settings.json")
        => _filePath = filePath;

    public OperatorSettings Load()
    {
        if (!File.Exists(_filePath)) return new OperatorSettings();
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<OperatorSettings>(json, _options) ?? new OperatorSettings();
    }

    public void Save(OperatorSettings settings)
        => File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, _options));
}
