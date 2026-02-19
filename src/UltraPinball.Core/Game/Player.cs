namespace UltraPinball.Core.Game;

/// <summary>
/// Represents one player. Holds score, extra balls, and arbitrary per-player
/// state so modes can persist data across balls without globals.
/// </summary>
public class Player
{
    public string Name { get; }
    public long Score { get; set; }
    public int ExtraBalls { get; set; }
    public TimeSpan GameTime { get; internal set; }

    private readonly Dictionary<string, object> _state = new();

    public Player(string name) => Name = name;

    /// <summary>Stores arbitrary per-player state keyed by name.</summary>
    public void SetState(string key, object value) => _state[key] = value;

    /// <summary>Retrieves per-player state, returning <paramref name="defaultValue"/> if absent.</summary>
    public T GetState<T>(string key, T defaultValue = default!) =>
        _state.TryGetValue(key, out var v) && v is T typed ? typed : defaultValue;

    public void AdjustState(string key, long delta)
    {
        var current = GetState<long>(key);
        _state[key] = current + delta;
    }

    public override string ToString() => $"{Name}: {Score:N0} pts";
}
