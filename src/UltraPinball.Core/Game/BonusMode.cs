using Microsoft.Extensions.Logging;

namespace UltraPinball.Core.Game;

/// <summary>
/// Built-in framework mode that awards end-of-ball bonus.
///
/// <para>
/// Register with <see cref="ModeLifecycle.Ball"/> so it is automatically added at
/// ball start and removed at ball end, resetting per-ball state each time.
/// During play, call <see cref="AddBonus"/> from scoring modes and optionally call
/// <see cref="SetMultiplier"/> to apply a multiplier.
/// </para>
/// <para>
/// Wire <see cref="TroughMode.BallDrained"/> to <see cref="StartBonus"/> to trigger
/// the countdown when the ball drains:
/// <code>trough.BallDrained += bonus.StartBonus;</code>
/// When the countdown completes, <see cref="GameController.EndBall"/> is called
/// automatically — do not call it from game code as well.
/// </para>
/// <para>
/// To skip bonus after a tilt, use a lambda instead of wiring directly:
/// <code>
/// trough.BallDrained += () =>
/// {
///     if (tilt.IsTilted) EndBall();
///     else bonus.StartBonus();
/// };
/// </code>
/// </para>
/// </summary>
public class BonusMode : Mode
{
    /// <inheritdoc />
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.Ball;

    // ── Configuration ──────────────────────────────────────────────────────────

    /// <summary>
    /// Points awarded to the player per countdown step.
    /// Defaults to 1 000. Set to match your game's bonus scale.
    /// </summary>
    public long StepAmount { get; set; } = 1_000;

    /// <summary>
    /// Seconds between countdown steps. Defaults to 0.1 s (10 ticks per second).
    /// </summary>
    public float StepIntervalSeconds { get; set; } = 0.1f;

    // ── Per-ball state ─────────────────────────────────────────────────────────

    private long _bonusValue;
    private int  _multiplier = 1;

    /// <summary>Total bonus accumulated this ball (before multiplier).</summary>
    public long BonusValue => _bonusValue;

    /// <summary>Current bonus multiplier. Always at least 1.</summary>
    public int Multiplier => _multiplier;

    // ── Countdown state ────────────────────────────────────────────────────────

    private long _remaining;
    private long _totalAwarded;

    // ── Events ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the bonus countdown begins.
    /// Parameters: <c>(baseBonus, multiplier)</c>.
    /// </summary>
    public event Action<long, int>? BonusStarted;

    /// <summary>
    /// Fired after each countdown step.
    /// Parameter: remaining value still to be awarded.
    /// </summary>
    public event Action<long>? BonusStep;

    /// <summary>
    /// Fired when the countdown completes and all bonus has been awarded.
    /// Parameter: total points awarded during this countdown.
    /// </summary>
    public event Action<long>? BonusCompleted;

    // ── Constructor ────────────────────────────────────────────────────────────

    public BonusMode() : base(priority: 5) { }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public override void ModeStarted()
    {
        _bonusValue = 0;
        _multiplier = 1;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Adds to the bonus accumulated this ball.</summary>
    public void AddBonus(long amount) => _bonusValue += amount;

    /// <summary>
    /// Sets the bonus multiplier for this ball.
    /// Values less than 1 are clamped to 1.
    /// </summary>
    public void SetMultiplier(int multiplier) => _multiplier = Math.Max(1, multiplier);

    // ── Bonus countdown ────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the bonus countdown. Wire this to <see cref="TroughMode.BallDrained"/>.
    /// Calls <see cref="GameController.EndBall"/> when the countdown completes.
    /// </summary>
    public void StartBonus()
    {
        _remaining    = _bonusValue * _multiplier;
        _totalAwarded = 0;

        Log.LogInformation("Bonus countdown: {Base} × {Mult} = {Total}",
            _bonusValue, _multiplier, _remaining);
        Game.Media?.Post(MediaEvents.BonusStarted,
            new { bonus = _bonusValue, multiplier = _multiplier, total = _remaining });
        BonusStarted?.Invoke(_bonusValue, _multiplier);

        if (_remaining <= 0)
        {
            Complete();
            return;
        }

        Delay(StepIntervalSeconds, OnStep, "bonus_step");
    }

    private void OnStep()
    {
        var award   = Math.Min(StepAmount, _remaining);
        _remaining    -= award;
        _totalAwarded += award;

        if (Game.CurrentPlayer != null)
            Game.CurrentPlayer.Score += award;

        Log.LogDebug("Bonus step: +{Award}, remaining {Remaining}", award, _remaining);
        Game.Media?.Post(MediaEvents.BonusStep, new { awarded = award, remaining = _remaining });
        BonusStep?.Invoke(_remaining);

        if (_remaining > 0)
            Delay(StepIntervalSeconds, OnStep, "bonus_step");
        else
            Complete();
    }

    private void Complete()
    {
        Log.LogInformation("Bonus complete — {Total} awarded.", _totalAwarded);
        Game.Media?.Post(MediaEvents.BonusCompleted, new { awarded = _totalAwarded });
        BonusCompleted?.Invoke(_totalAwarded);
        Game.EndBall();
    }
}
