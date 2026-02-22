namespace UltraPinball.Core.Devices;

[Flags]
public enum SwitchTags
{
    None        = 0,
    Playfield   = 1 << 0,  // resets ball search timer
    Trough      = 1 << 1,  // drain detection
    Eos         = 1 << 2,  // flipper end-of-stroke
    ShooterLane = 1 << 3,  // ball is safely held here; suspends ball search
    UserButton  = 1 << 4,  // player-facing button (flippers, Start, Launch, etc.)
    Tilt        = 1 << 5,  // tilt bob switch
    SlamTilt    = 1 << 6,  // slam tilt switch
    Service     = 1 << 7,  // service door or button; entry point for ServiceMode
}
