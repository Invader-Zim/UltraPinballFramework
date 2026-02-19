using UltraPinball.Core.Devices;
using Microsoft.Extensions.Logging;

namespace UltraPinball.Core.Game;

/// <summary>
/// How a switch event should be interpreted when registering a handler.
/// </summary>
public enum SwitchActivation
{
    /// <summary>Switch moved to its active state (closed for NO, open for NC).</summary>
    Active,
    /// <summary>Switch moved to its inactive state.</summary>
    Inactive,
    /// <summary>Switch physically closed (regardless of NO/NC type).</summary>
    Closed,
    /// <summary>Switch physically opened (regardless of NO/NC type).</summary>
    Open
}

/// <summary>Returned by a switch handler to control event propagation.</summary>
public enum SwitchHandlerResult { Continue, Stop }

/// <summary>
/// Base class for all game modes. A mode is a self-contained unit of game behavior
/// that can be added to or removed from the ModeQueue at runtime.
///
/// Modes react to switch events via registered handlers and can schedule
/// delayed callbacks via the Delay system.
///
/// Priority determines event routing order: higher priority modes see events first
/// and can stop them from reaching lower priority modes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Event routing:</b> When a switch event fires, <see cref="ModeQueue"/> dispatches it
/// to modes in descending priority order. Each handler returns either
/// <see cref="SwitchHandlerResult.Continue"/> (pass the event to lower-priority modes)
/// or <see cref="SwitchHandlerResult.Stop"/> (consume it). Returning <c>Stop</c> is useful
/// when a high-priority mode "owns" a switch for the duration of a feature — for example,
/// a lock mode consuming the ball-enter switch so the base game doesn't also react.
/// </para>
/// <para>
/// <b>Lifecycle:</b> Override <see cref="ModeStarted"/> to register handlers and set up state.
/// Override <see cref="ModeStopped"/> to clean up. Override <see cref="Tick"/> for per-frame logic.
/// </para>
/// <para>
/// <b>Delays:</b> Use <see cref="Delay"/> instead of <c>Task.Delay</c> or timers.
/// Delays are checked by <see cref="ModeQueue"/> on the main game-loop thread, so callbacks
/// run safely without locking. Timed switch handlers automatically cancel their delay if the
/// switch returns to its previous state before the delay elapses.
/// </para>
/// </remarks>
public abstract class Mode
{
    public int Priority { get; }
    public GameController Game { get; private set; } = null!;

    private readonly List<RegisteredHandler> _handlers = new();
    private readonly List<PendingDelay> _delays = new();
    private ILogger? _log;

    protected ILogger Log => _log ??= Game.CreateLogger(GetType().Name);

    protected Mode(int priority) => Priority = priority;

    internal void AttachGame(GameController game) => Game = game;

    // ── Switch handler registration ───────────────────────────────────────────

    /// <summary>
    /// Registers a switch event handler that fires when the named switch enters the specified activation state.
    /// </summary>
    /// <param name="switchName">Symbolic name as declared in <see cref="MachineConfig"/>.</param>
    /// <param name="activation">Which state transition triggers this handler.</param>
    /// <param name="handler">
    /// Callback invoked when the switch activates. Return <see cref="SwitchHandlerResult.Stop"/>
    /// to consume the event and prevent lower-priority modes from seeing it, or
    /// <see cref="SwitchHandlerResult.Continue"/> to let it propagate.
    /// </param>
    /// <param name="delaySeconds">
    /// If greater than zero, the handler fires only after the switch has been held in this
    /// activation state for the specified duration. The pending delay is automatically cancelled
    /// if the switch returns to its previous state before the timer elapses — useful for
    /// "held for X seconds" behaviours such as service-menu entry or hold-to-drain detection.
    /// </param>
    protected void AddSwitchHandler(string switchName, SwitchActivation activation,
                                    Func<Switch, SwitchHandlerResult> handler,
                                    float delaySeconds = 0f)
    {
        _handlers.Add(new RegisteredHandler(switchName, activation, handler, delaySeconds));
    }

    /// <summary>
    /// Convenience overload for handlers that always return <see cref="SwitchHandlerResult.Continue"/>.
    /// </summary>
    /// <inheritdoc cref="AddSwitchHandler(string, SwitchActivation, Func{Switch, SwitchHandlerResult}, float)"/>
    protected void AddSwitchHandler(string switchName, SwitchActivation activation,
                                    Action<Switch> handler, float delaySeconds = 0f)
    {
        AddSwitchHandler(switchName, activation, sw => { handler(sw); return SwitchHandlerResult.Continue; }, delaySeconds);
    }

    // ── Delay system ──────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules <paramref name="callback"/> to be invoked after <paramref name="seconds"/> have elapsed.
    /// </summary>
    /// <param name="seconds">How long to wait before firing the callback.</param>
    /// <param name="callback">Action to invoke when the delay elapses. Runs on the game-loop thread.</param>
    /// <param name="name">
    /// Optional stable name for the delay. If a delay with the same name already exists it is
    /// replaced, effectively restarting the timer. Pass an explicit name when you need to cancel
    /// or restart the delay later via <see cref="CancelDelay"/>.
    /// </param>
    /// <returns>The name of the scheduled delay, generated if not supplied.</returns>
    protected string Delay(float seconds, Action callback, string? name = null)
    {
        var delayName = name ?? $"delay_{Guid.NewGuid():N}";
        _delays.Add(new PendingDelay(delayName, DateTime.UtcNow.AddSeconds(seconds),
                                     callback, CancelTrigger: null));
        return delayName;
    }

    /// <summary>Cancels a previously scheduled delay by name. Safe to call if name doesn't exist.</summary>
    protected void CancelDelay(string name) =>
        _delays.RemoveAll(d => d.Name == name);

    protected bool IsDelayed(string name) => _delays.Any(d => d.Name == name);

    // ── Lifecycle callbacks ───────────────────────────────────────────────────

    /// <summary>Called by ModeQueue when this mode becomes active.</summary>
    public virtual void ModeStarted() { }

    /// <summary>Called by ModeQueue when this mode is removed.</summary>
    public virtual void ModeStopped() { }

    /// <summary>Called every game loop iteration while active.</summary>
    public virtual void Tick(float deltaSeconds) { }

    // ── Internal: called by ModeQueue ────────────────────────────────────────

    /// <summary>
    /// Processes an incoming switch event. Returns Stop if this mode consumed
    /// the event and lower-priority modes should not see it.
    /// </summary>
    internal SwitchHandlerResult HandleSwitchEvent(Switch sw, SwitchState newState)
    {
        // Cancel any timed delays for this switch that are waiting for the opposite state
        _delays.RemoveAll(d =>
            d.CancelTrigger != null &&
            d.CancelTrigger.SwitchName == sw.Name &&
            !IsMatchingActivation(sw, d.CancelTrigger.Activation, newState));

        var result = SwitchHandlerResult.Continue;

        foreach (var h in _handlers)
        {
            if (h.SwitchName != sw.Name) continue;
            if (!IsMatchingActivation(sw, h.Activation, newState)) continue;

            if (h.DelaySeconds <= 0f)
            {
                if (h.Handler(sw) == SwitchHandlerResult.Stop)
                    result = SwitchHandlerResult.Stop;
            }
            else
            {
                // Set up a timed delay that cancels if the switch deactivates
                var cancelActivation = h.Activation == SwitchActivation.Active
                    ? SwitchActivation.Inactive
                    : h.Activation == SwitchActivation.Inactive
                        ? SwitchActivation.Active
                        : h.Activation == SwitchActivation.Closed
                            ? SwitchActivation.Open
                            : SwitchActivation.Closed;

                var delayName = $"sw_{sw.Name}_{h.Activation}_{h.DelaySeconds}s";
                CancelDelay(delayName); // restart if already pending
                _delays.Add(new PendingDelay(
                    delayName,
                    DateTime.UtcNow.AddSeconds(h.DelaySeconds),
                    () => h.Handler(sw),
                    CancelTrigger: new SwitchCancelTrigger(sw.Name, cancelActivation)));
            }
        }

        return result;
    }

    /// <summary>Fires any delays whose time has elapsed. Called each game loop tick.</summary>
    internal void DispatchDelays()
    {
        var now = DateTime.UtcNow;
        var toFire = _delays.Where(d => d.FireAt <= now).ToList();
        foreach (var d in toFire)
        {
            _delays.Remove(d);
            d.Callback();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsMatchingActivation(Switch sw, SwitchActivation activation, SwitchState newState)
    {
        return activation switch
        {
            SwitchActivation.Active   => newState == SwitchState.Closed && sw.Type == SwitchType.NormallyOpen
                                      || newState == SwitchState.Open   && sw.Type == SwitchType.NormallyClosed,
            SwitchActivation.Inactive => newState == SwitchState.Open   && sw.Type == SwitchType.NormallyOpen
                                      || newState == SwitchState.Closed && sw.Type == SwitchType.NormallyClosed,
            SwitchActivation.Closed   => newState == SwitchState.Closed,
            SwitchActivation.Open     => newState == SwitchState.Open,
            _ => false
        };
    }

    // ── Private data structures ───────────────────────────────────────────────

    private record RegisteredHandler(
        string SwitchName,
        SwitchActivation Activation,
        Func<Switch, SwitchHandlerResult> Handler,
        float DelaySeconds);

    private record SwitchCancelTrigger(string SwitchName, SwitchActivation Activation);

    private record PendingDelay(
        string Name,
        DateTime FireAt,
        Action Callback,
        SwitchCancelTrigger? CancelTrigger);

    public override string ToString() => $"{GetType().Name} (pri={Priority})";
}
