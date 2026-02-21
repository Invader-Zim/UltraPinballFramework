namespace UltraPinball.Core.Game;

/// <summary>
/// Represents one player. Holds score, extra balls, and arbitrary per-player
/// state so modes can persist data across balls without globals.
///
/// <para>
/// Two state scopes are available:
/// <list type="bullet">
///   <item><b>Game state</b> (<see cref="SetState"/>, <see cref="GetState{T}"/>, <see cref="Increment"/>) —
///   persists for the player's entire game, across all balls.</item>
///   <item><b>Ball state</b> (<see cref="SetBallState"/>, <see cref="GetBallState{T}"/>, <see cref="IncrementBallState"/>) —
///   automatically cleared by <see cref="GameController"/> at the start of each new ball.
///   Use this for things like "hits this ball" or "bonus multiplier earned this ball".</item>
/// </list>
/// </para>
/// </summary>
public class Player
{
    public string Name { get; }
    public long Score { get; set; }
    public int ExtraBalls { get; set; }
    public TimeSpan GameTime { get; internal set; }

    private readonly Dictionary<string, object> _state     = new();
    private readonly Dictionary<string, object> _ballState = new();

    public Player(string name) => Name = name;

    // ── Game-scoped state (persists all balls) ────────────────────────────────

    /// <summary>Stores an arbitrary game-state value keyed by name.</summary>
    public void SetState(string key, object value) => _state[key] = value;

    /// <summary>Retrieves a game-state value, returning <paramref name="defaultValue"/> if absent.</summary>
    public T GetState<T>(string key, T defaultValue = default!) =>
        _state.TryGetValue(key, out var v) && v is T typed ? typed : defaultValue;

    /// <summary>
    /// Increments a long game-state variable by <paramref name="delta"/>.
    /// Creates it at 0 if it does not yet exist.
    /// </summary>
    public void Increment(string key, long delta = 1) =>
        _state[key] = GetState<long>(key) + delta;

    // ── Ball-scoped state (cleared at the start of each ball) ─────────────────

    /// <summary>
    /// Stores per-ball state that is automatically cleared at the start of each new ball.
    /// </summary>
    public void SetBallState(string key, object value) => _ballState[key] = value;

    /// <summary>
    /// Retrieves per-ball state, returning <paramref name="defaultValue"/> if absent or
    /// after the state was cleared at ball start.
    /// </summary>
    public T GetBallState<T>(string key, T defaultValue = default!) =>
        _ballState.TryGetValue(key, out var v) && v is T typed ? typed : defaultValue;

    /// <summary>
    /// Increments a long ball-state variable by <paramref name="delta"/>.
    /// Creates it at 0 if it does not yet exist.
    /// </summary>
    public void IncrementBallState(string key, long delta = 1) =>
        _ballState[key] = GetBallState<long>(key) + delta;

    /// <summary>Clears all ball-scoped state. Called by <see cref="GameController"/> at the start of each ball.</summary>
    internal void ResetBallState() => _ballState.Clear();

    public override string ToString() => $"{Name}: {Score:N0} pts";
}
