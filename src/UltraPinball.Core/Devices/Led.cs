using UltraPinball.Core.Platform;

namespace UltraPinball.Core.Devices;

/// <summary>Represents an addressable RGB LED.</summary>
public class Led
{
    public string Name { get; }
    public int HwAddress { get; }

    private readonly IHardwarePlatform _platform;

    internal Led(string name, int hwAddress, IHardwarePlatform platform)
    {
        Name = name;
        HwAddress = hwAddress;
        _platform = platform;
    }

    /// <summary>Sets this LED to the given RGB colour.</summary>
    /// <param name="r">Red component (0–255).</param>
    /// <param name="g">Green component (0–255).</param>
    /// <param name="b">Blue component (0–255).</param>
    public void SetColor(byte r, byte g, byte b) =>
        _platform.SetLedColor(HwAddress, r, g, b);

    /// <summary>Sets this LED to the given RGB colour expressed as a value tuple.</summary>
    public void SetColor((byte r, byte g, byte b) color) =>
        _platform.SetLedColor(HwAddress, color.r, color.g, color.b);

    /// <summary>Turns this LED off (sets all channels to 0).</summary>
    public void Off() => _platform.SetLedColor(HwAddress, 0, 0, 0);

    public override string ToString() => $"{Name} (hw=0x{HwAddress:X2})";
}
