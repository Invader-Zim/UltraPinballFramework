using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Always-on mode (priority 1). Waits for the Start button and calls
/// StartGame(). Re-announces after each game ends.
/// </summary>
public class AttractMode : Mode
{
    public AttractMode() : base(priority: 1) { }

    public override void ModeStarted()
    {
        Log.LogInformation("[ATTRACT] Waiting for start button...");
        AddSwitchHandler("Start", SwitchActivation.Active, OnStartPressed);
        Game.GameEnded += OnGameEnded;
    }

    public override void ModeStopped()
    {
        Game.GameEnded -= OnGameEnded;
    }

    private SwitchHandlerResult OnStartPressed(Switch sw)
    {
        if (Game.IsGameInProgress)
            return SwitchHandlerResult.Continue;

        Log.LogInformation("[ATTRACT] Start — starting game!");
        Game.StartGame();
        return SwitchHandlerResult.Stop;
    }

    private void OnGameEnded() =>
        Log.LogInformation("[ATTRACT] Game over. Waiting for start button...");
}
