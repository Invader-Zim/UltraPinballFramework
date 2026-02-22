using UltraPinball.Core.Platform;

namespace UltraPinball.Core.Devices;

/// <summary>
/// Represents a solenoid coil (kicker, bumper, trough eject, flipper main, etc.).
/// </summary>
public class Coil
{
    public string Name { get; }
    public int HwNumber { get; }
    public int DefaultPulseMs { get; }

    private readonly IHardwarePlatform _platform;

    internal Coil(string name, int hwNumber, int defaultPulseMs, IHardwarePlatform platform)
    {
        Name = name;
        HwNumber = hwNumber;
        DefaultPulseMs = defaultPulseMs;
        _platform = platform;
    }

    // ── Software gate ─────────────────────────────────────────────────────────

    /// <summary>
    /// <c>true</c> by default. When <c>false</c>, <see cref="Pulse"/> and
    /// <see cref="Hold"/> are no-ops. Use <see cref="Enable"/> and
    /// <see cref="Disable"/> to control this flag.
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Allows the coil to fire. Call this to re-arm after a <see cref="Disable"/>.
    /// Does not energise the coil — call <see cref="Pulse"/> or <see cref="Hold"/>
    /// afterwards.
    /// </summary>
    public void Enable() => IsEnabled = true;

    /// <summary>
    /// Prevents the coil from firing and cuts power immediately.
    /// Call <see cref="Enable"/> to re-arm.
    /// Use this for tilt, service mode, or any state where the coil must
    /// not fire until explicitly unlocked.
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        _platform.DisableCoil(HwNumber);
    }

    // ── Hardware operations ───────────────────────────────────────────────────

    /// <summary>
    /// Fires the coil for the given duration, then cuts power.
    /// No-op when <see cref="IsEnabled"/> is <c>false</c>.
    /// </summary>
    /// <param name="milliseconds">
    /// Pulse duration in milliseconds. If omitted, <see cref="DefaultPulseMs"/> is used.
    /// </param>
    public void Pulse(int? milliseconds = null)
    {
        if (!IsEnabled) return;
        _platform.PulseCoil(HwNumber, milliseconds ?? DefaultPulseMs);
    }

    /// <summary>
    /// Holds the coil on continuously. No-op when <see cref="IsEnabled"/> is <c>false</c>.
    /// Always pair with <see cref="Disable"/> or use <see cref="Game.Mode.HoldCoilFor"/>
    /// to avoid burning out the winding. Hardware rules are preferred for hold scenarios.
    /// </summary>
    public void Hold()
    {
        if (!IsEnabled) return;
        _platform.HoldCoil(HwNumber);
    }

    /// <summary>
    /// Cuts power to the coil without affecting <see cref="IsEnabled"/>.
    /// Used internally by <see cref="Game.Mode.HoldCoilFor"/> to release a timed hold
    /// while keeping the coil available for future firing.
    /// </summary>
    internal void CutPower() => _platform.DisableCoil(HwNumber);

    public override string ToString() => $"{Name} (hw=0x{HwNumber:X2}, pulse={DefaultPulseMs}ms)";
}
