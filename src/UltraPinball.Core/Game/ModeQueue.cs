using UltraPinball.Core.Devices;
using Microsoft.Extensions.Logging;

namespace UltraPinball.Core.Game;

/// <summary>
/// Maintains an ordered list of active modes, sorted by priority (highest first).
/// Switch events flow through modes in priority order; a mode returning Stop
/// prevents lower-priority modes from seeing the event.
/// </summary>
public class ModeQueue
{
    private readonly List<Mode> _modes = new();
    private readonly GameController _game;
    private readonly ILogger<ModeQueue> _log;

    internal ModeQueue(GameController game, ILogger<ModeQueue> log)
    {
        _game = game;
        _log = log;
    }

    public IReadOnlyList<Mode> ActiveModes => _modes;

    public void Add(Mode mode)
    {
        if (_modes.Contains(mode))
            throw new InvalidOperationException($"Mode {mode} is already in the queue.");

        mode.AttachGame(_game);
        _modes.Add(mode);
        _modes.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // descending
        _log.LogDebug("Mode added: {Mode}", mode);
        mode.ModeStarted();
    }

    public void Remove(Mode mode)
    {
        if (!_modes.Remove(mode))
            return;
        _log.LogDebug("Mode removed: {Mode}", mode);
        mode.ModeStopped();
    }

    public bool Contains(Mode mode) => _modes.Contains(mode);

    public bool Contains<T>() where T : Mode => _modes.OfType<T>().Any();

    public T? Get<T>() where T : Mode => _modes.OfType<T>().FirstOrDefault();

    /// <summary>Routes a switch event through all active modes in priority order.</summary>
    internal void HandleSwitchEvent(Switch sw, SwitchState newState)
    {
        // Snapshot to guard against modes being added/removed during dispatch
        var snapshot = _modes.ToList();
        foreach (var mode in snapshot)
        {
            var result = mode.HandleSwitchEvent(sw, newState);
            if (result == SwitchHandlerResult.Stop)
                break;
        }
    }

    /// <summary>Ticks all active modes and dispatches elapsed delays.</summary>
    internal void Tick(float deltaSeconds)
    {
        var snapshot = _modes.ToList();
        foreach (var mode in snapshot)
        {
            mode.DispatchDelays();
            mode.Tick(deltaSeconds);
        }
    }
}
