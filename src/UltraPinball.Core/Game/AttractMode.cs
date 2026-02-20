using Microsoft.Extensions.Logging;
using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// Default framework attract mode (priority 1). Always active; waits for Start
/// to begin a game, allows additional players to join during Ball 1, and hands
/// off to <see cref="GameOverMode"/> when a game ends.
///
/// <para>
/// Override <see cref="OnGameEnded"/> or <see cref="CreateGameOverMode"/> to
/// customise post-game behaviour. Override <see cref="OnGameOverCompleted"/> to
/// react when the game-over dwell finishes and attract resumes.
/// </para>
/// </summary>
public class AttractMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

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

    // ── Switch handlers ───────────────────────────────────────────────────────

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

    // ── Overridable game-ended behaviour ──────────────────────────────────────

    /// <summary>
    /// Called when <see cref="GameController.GameEnded"/> fires.
    /// Default implementation creates a <see cref="GameOverMode"/> via
    /// <see cref="CreateGameOverMode"/> and adds it to the mode queue.
    /// </summary>
    protected virtual void OnGameEnded()
    {
        var finalPlayers = Game.Players.ToList();
        var gameOver = CreateGameOverMode(finalPlayers);
        gameOver.Completed += OnGameOverCompleted;
        Game.Modes.Add(gameOver);
    }

    /// <summary>
    /// Factory method for the post-game mode. Override to substitute a custom
    /// game-over mode without replacing the whole <see cref="OnGameEnded"/> flow.
    /// </summary>
    protected virtual GameOverMode CreateGameOverMode(IReadOnlyList<Player> finalPlayers)
        => new GameOverMode(finalPlayers);

    /// <summary>
    /// Called when the game-over dwell completes naturally (i.e. the player did
    /// not press Start to skip it). Override to trigger attract animations, etc.
    /// </summary>
    protected virtual void OnGameOverCompleted()
        => Log.LogInformation("[ATTRACT] Waiting for start button...");
}
