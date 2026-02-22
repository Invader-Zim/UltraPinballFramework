using UltraPinball.Core.Game;
using Microsoft.Extensions.Logging.Abstractions;

namespace UltraPinball.Tests;

public class ChildModeTests
{
    // ── Build helper ──────────────────────────────────────────────────────────

    private static (GameController game, ChildParentMode parent, ChildSubMode child) Build()
    {
        var machine = new EmptyMachine();
        machine.Initialize(new NullPlatform());
        var game   = new GameController(machine, new NullPlatform(), NullLoggerFactory.Instance);
        var parent = new ChildParentMode();
        var child  = new ChildSubMode();
        game.RegisterMode(parent);
        return (game, parent, child);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddChildMode_AddsChildToQueue()
    {
        var (game, parent, child) = Build();

        parent.StartChild(child);

        Assert.True(game.Modes.Contains(child));
    }

    [Fact]
    public void RemoveChildMode_RemovesChildFromQueue()
    {
        var (game, parent, child) = Build();
        parent.StartChild(child);

        parent.StopChild(child);

        Assert.False(game.Modes.Contains(child));
    }

    [Fact]
    public void ParentDeactivated_AutoRemovesChild()
    {
        var (game, parent, child) = Build();
        parent.StartChild(child);

        game.Modes.Remove(parent);

        Assert.False(game.Modes.Contains(child));
    }

    [Fact]
    public void AddChildMode_IsIdempotent()
    {
        var (game, parent, child) = Build();

        parent.StartChild(child);
        parent.StartChild(child);  // second call must be a no-op

        var count = game.Modes.ActiveModes.Count(m => m == child);
        Assert.Equal(1, count);
    }
}

// ── Test-only helper modes ────────────────────────────────────────────────────

class ChildParentMode : Mode
{
    public ChildParentMode() : base(priority: 50) { }
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    public void StartChild(Mode child) => AddChildMode(child);
    public void StopChild(Mode child)  => RemoveChildMode(child);
}

class ChildSubMode : Mode
{
    public ChildSubMode() : base(priority: 40) { }
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;
}
