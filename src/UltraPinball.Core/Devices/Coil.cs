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

    /// <summary>Fires the coil for the given duration, then cuts power.</summary>
    /// <param name="milliseconds">
    /// Pulse duration in milliseconds. If omitted, <see cref="DefaultPulseMs"/> is used.
    /// </param>
    public void Pulse(int? milliseconds = null) =>
        _platform.PulseCoil(HwNumber, milliseconds ?? DefaultPulseMs);

    /// <summary>
    /// Holds the coil on continuously. Use with caution — always pair with Disable()
    /// to avoid burning out the coil. Hardware rules are preferred for hold scenarios.
    /// </summary>
    public void Hold() => _platform.HoldCoil(HwNumber);

    /// <summary>Cuts power to the coil immediately.</summary>
    public void Disable() => _platform.DisableCoil(HwNumber);

    public override string ToString() => $"{Name} (hw=0x{HwNumber:X2}, pulse={DefaultPulseMs}ms)";
}
