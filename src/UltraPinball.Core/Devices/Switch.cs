namespace UltraPinball.Core.Devices;

/// <summary>
/// Represents a physical switch (button, opto, rollover, etc.).
/// State is updated by GameController in response to hardware events.
/// </summary>
public class Switch
{
    public string Name { get; }
    public int HwNumber { get; }
    public SwitchType Type { get; }
    public bool Debounce { get; }
    public SwitchTags Tags { get; }

    public SwitchState State { get; internal set; } = SwitchState.Open;
    public DateTime LastChangedAt { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// True when the switch is in its active/triggered state, regardless of
    /// whether it is normally-open or normally-closed.
    /// </summary>
    public bool IsActive => Type == SwitchType.NormallyOpen
        ? State == SwitchState.Closed
        : State == SwitchState.Open;

    public TimeSpan TimeSinceChanged => DateTime.UtcNow - LastChangedAt;

    /// <summary>Creates a new switch descriptor. Called by <see cref="UltraPinball.Core.Game.MachineConfig.AddSwitch"/>.</summary>
    /// <param name="name">Symbolic name used to look up this switch (e.g. <c>"LeftFlipper"</c>).</param>
    /// <param name="hwNumber">Hardware address on the controller board.</param>
    /// <param name="type">Whether the switch is normally-open or normally-closed at rest.</param>
    /// <param name="debounce">
    /// When <c>true</c>, the platform applies hardware debouncing before reporting state changes.
    /// Set to <c>false</c> for flipper buttons, which require the fastest possible response.
    /// </param>
    /// <param name="tags">Optional semantic role flags used for tag-based queries.</param>
    public Switch(string name, int hwNumber,
                  SwitchType type = SwitchType.NormallyOpen,
                  bool debounce = true,
                  SwitchTags tags = SwitchTags.None)
    {
        Name = name;
        HwNumber = hwNumber;
        Type = type;
        Debounce = debounce;
        Tags = tags;
    }

    public override string ToString() =>
        $"{Name} (hw=0x{HwNumber:X2}, {State}, active={IsActive})";
}
