using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Post-game mode (priority 15). Displays final scores for a brief dwell period,
/// then removes itself and returns control to <see cref="AttractMode"/>.
///
/// <para>
/// Pressing Start during the dwell skips immediately to a new game: this mode
/// dismisses itself and returns <see cref="SwitchHandlerResult.Continue"/> so
/// <see cref="AttractMode"/> sees the event and calls <see cref="GameController.StartGame"/>.
/// </para>
/// </summary>
public class GameOverMode : Mode
{
    /// <summary>
    /// Fired when the dwell timer elapses and this mode removes itself naturally.
    /// Not fired when Start is pressed (the game begins immediately in that case).
    /// </summary>
    public event Action? Completed;

    private readonly IReadOnlyList<Player> _finalPlayers;

    /// <summary>How long to display the final scores before returning to attract.</summary>
    public float DwellSeconds { get; set; } = 12f;

    public GameOverMode(IReadOnlyList<Player> finalPlayers) : base(priority: 15)
    {
        _finalPlayers = finalPlayers.ToList();
    }

    public override void ModeStarted()
    {
        Log.LogInformation("[GAME OVER] Game over!");
        for (var i = 0; i < _finalPlayers.Count; i++)
            Log.LogInformation("[GAME OVER]   {Player}: {Score:N0}",
                _finalPlayers[i].Name, _finalPlayers[i].Score);

        AddSwitchHandler("Start", SwitchActivation.Active, OnStartPressed);
        Delay(DwellSeconds, OnDwellElapsed, name: "game_over_dwell");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private SwitchHandlerResult OnStartPressed(Switch sw)
    {
        // Dismiss without firing Completed — the game is starting immediately.
        Dismiss(completed: false);
        // Return Continue so AttractMode sees the Start event and calls StartGame().
        return SwitchHandlerResult.Continue;
    }

    private void OnDwellElapsed()
    {
        Log.LogInformation("[GAME OVER] Returning to attract.");
        Dismiss(completed: true);
    }

    private void Dismiss(bool completed)
    {
        Game.Modes.Remove(this);
        if (completed)
            Completed?.Invoke();
    }
}
