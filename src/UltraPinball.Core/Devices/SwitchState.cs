namespace UltraPinball.Core.Devices;

public enum SwitchState { Open, Closed }

public enum SwitchType
{
    /// <summary>Normal: open at rest, closed when activated. Most switches.</summary>
    NormallyOpen,
    /// <summary>Inverted: closed at rest, open when activated. Optos, some ball trough sensors.</summary>
    NormallyClosed
}
