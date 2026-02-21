using UltraPinball.Core.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class PlayerVarTests
{
    // ── Unit tests on Player directly ─────────────────────────────────────────

    [Fact]
    public void GetState_ReturnsDefault_WhenNotSet()
    {
        var player = new Player("P1");
        Assert.Equal(0, player.GetState<int>("x"));
    }

    [Fact]
    public void SetState_AndGet_RoundTrip()
    {
        var player = new Player("P1");
        player.SetState("x", 42);
        Assert.Equal(42, player.GetState<int>("x"));
    }

    [Fact]
    public void Increment_DefaultDeltaIsOne()
    {
        var player = new Player("P1");
        player.Increment("x");
        player.Increment("x");
        Assert.Equal(2L, player.GetState<long>("x"));
    }

    [Fact]
    public void Increment_CustomDelta()
    {
        var player = new Player("P1");
        player.Increment("x", 10);
        Assert.Equal(10L, player.GetState<long>("x"));
    }

    [Fact]
    public void GetBallState_ReturnsDefault_WhenNotSet()
    {
        var player = new Player("P1");
        Assert.Equal(0, player.GetBallState<int>("x"));
    }

    [Fact]
    public void SetBallState_AndGet_RoundTrip()
    {
        var player = new Player("P1");
        player.SetBallState("x", 7);
        Assert.Equal(7, player.GetBallState<int>("x"));
    }

    [Fact]
    public void IncrementBallState_Works()
    {
        var player = new Player("P1");
        player.IncrementBallState("x");
        player.IncrementBallState("x");
        Assert.Equal(2L, player.GetBallState<long>("x"));
    }

    // ── Integration tests via GameController lifecycle ────────────────────────

    /// <summary>
    /// Builds a minimal game (no modes, no switches) to exercise
    /// StartGame → EndBall → StartBall lifecycle.
    /// </summary>
    private static GameController BuildGame()
    {
        var machine = new EmptyMachine();
        machine.Initialize(new NullPlatform());
        return new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
    }

    [Fact]
    public void GameState_PersistsAcrossBalls()
    {
        var game = BuildGame();
        game.StartGame();                              // Ball 1

        game.CurrentPlayer!.SetState("jackpots", 5);

        game.EndBall();                                // Ball 2 begins

        Assert.Equal(5, game.CurrentPlayer!.GetState<int>("jackpots"));
    }

    [Fact]
    public void BallState_ResetsOnNewBall()
    {
        var game = BuildGame();
        game.StartGame();                              // Ball 1

        game.CurrentPlayer!.SetBallState("hits", 3);
        Assert.Equal(3, game.CurrentPlayer.GetBallState<int>("hits"));

        game.EndBall();                                // Ball 2 begins — ball state clears

        Assert.Equal(0, game.CurrentPlayer!.GetBallState<int>("hits"));
    }
}

// ── Minimal test machine (no switches, no coils) ──────────────────────────────

class EmptyMachine : UltraPinball.Core.Game.MachineConfig
{
    public override void Configure() { }
}
