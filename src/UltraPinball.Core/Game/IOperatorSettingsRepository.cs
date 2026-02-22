namespace UltraPinball.Core.Game;

public interface IOperatorSettingsRepository
{
    /// <summary>Loads operator settings. Returns defaults if no settings have been saved.</summary>
    OperatorSettings Load();

    /// <summary>Persists operator settings.</summary>
    void Save(OperatorSettings settings);
}
