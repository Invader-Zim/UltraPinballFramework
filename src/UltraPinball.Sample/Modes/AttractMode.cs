using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using Switch = UltraPinball.Core.Devices.Switch;

namespace UltraPinball.Sample.Modes;

/// <summary>
/// Always-on mode (priority 1). Waits for the Start button and starts a game.
/// When a game ends, hands off to <see cref="GameOverMode"/> for the post-game
/// sequence, then resumes attract once it completes.
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
        if (!Game.IsGameInProgress)
        {
            Log.LogInformation("[ATTRACT] Start — starting game!");
            Game.StartGame();
            return SwitchHandlerResult.Stop;
        }

        if (Game.Ball == 1 && Game.Players.Count < Game.MaxPlayers)
        {
            var player = Game.AddPlayer();
            Log.LogInformation("[ATTRACT] Player {Number} added.", Game.Players.Count);
            Game.Media?.Post("player_added", new { player = player.Name, total_players = Game.Players.Count });
            return SwitchHandlerResult.Stop;
        }

        return SwitchHandlerResult.Continue;
    }

    private void OnGameEnded()
    {
        // Snapshot players now — they persist until the next StartGame().
        var finalPlayers = Game.Players.ToList();
        var gameOver = new GameOverMode(finalPlayers);

        // When GameOver auto-dismisses (dwell), re-announce attract.
        // If the player pressed Start to dismiss it, AttractMode will log
        // "starting game!" instead, so this callback never fires in that path.
        gameOver.Completed += () => Log.LogInformation("[ATTRACT] Waiting for start button...");

        Game.Modes.Add(gameOver);
    }
}
