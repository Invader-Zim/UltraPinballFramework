using UltraPinball.Core.Devices;
using UltraPinball.Core.Game;

namespace UltraPinball.Sample;

/// <summary>
/// A typical 5-ball-trough playfield used as the framework reference machine.
///
/// Layout:
///   Trough (5 NC opto switches + eject coil) → Shooter lane → Auto-launch coil
///   Left side:  outlane, inlane, flipper (main+hold), EOS, sling
///   Right side: mirror of left
/// </summary>
public class SampleMachine : MachineConfig
{
    public override void Configure()
    {
        ConfigureSwitches();
        ConfigureCoils();
        ConfigureHardwareRules();
    }

    private void ConfigureSwitches()
    {
        // ── Ball path ─────────────────────────────────────────────────────────
        AddSwitch("ShooterLane",    hwNumber: 0x00, tags: SwitchTags.ShooterLane);

        // ── Left side ─────────────────────────────────────────────────────────
        AddSwitch("LeftOutlane",    hwNumber: 0x01, tags: SwitchTags.Playfield);
        AddSwitch("LeftInlane",     hwNumber: 0x02, tags: SwitchTags.Playfield);
        AddSwitch("LeftFlipperEos", hwNumber: 0x03, tags: SwitchTags.Eos);
        AddSwitch("LeftSling",      hwNumber: 0x04, tags: SwitchTags.Playfield);  // two physical switches in parallel → one game switch
        AddSwitch("LeftFlipper",    hwNumber: 0x05, debounce: false, tags: SwitchTags.UserButton);

        // ── Right side ────────────────────────────────────────────────────────
        AddSwitch("RightOutlane",    hwNumber: 0x06, tags: SwitchTags.Playfield);
        AddSwitch("RightInlane",     hwNumber: 0x07, tags: SwitchTags.Playfield);
        AddSwitch("RightFlipperEos", hwNumber: 0x08, tags: SwitchTags.Eos);
        AddSwitch("RightSling",      hwNumber: 0x09, tags: SwitchTags.Playfield);
        AddSwitch("RightFlipper",    hwNumber: 0x0A, debounce: false, tags: SwitchTags.UserButton);

        // ── Cabinet ───────────────────────────────────────────────────────────
        AddSwitch("Start",          hwNumber: 0x0B, tags: SwitchTags.UserButton);
        AddSwitch("Tilt",           hwNumber: 0x0C, tags: SwitchTags.Tilt);
        AddSwitch("SlamTilt",       hwNumber: 0x0D, tags: SwitchTags.SlamTilt);
        AddSwitch("ServiceButton",  hwNumber: 0x0E, tags: SwitchTags.Service);

        // ── Trough (5-ball, normally-closed opto switches) ────────────────────
        // Active = Open = beam broken = ball present.
        // Trough0 is nearest the eject coil; Trough4 is farthest (drain end).
        AddSwitch("Trough0", hwNumber: 0x10, type: SwitchType.NormallyClosed, tags: SwitchTags.Trough);
        AddSwitch("Trough1", hwNumber: 0x11, type: SwitchType.NormallyClosed, tags: SwitchTags.Trough);
        AddSwitch("Trough2", hwNumber: 0x12, type: SwitchType.NormallyClosed, tags: SwitchTags.Trough);
        AddSwitch("Trough3", hwNumber: 0x13, type: SwitchType.NormallyClosed, tags: SwitchTags.Trough);
        AddSwitch("Trough4", hwNumber: 0x14, type: SwitchType.NormallyClosed, tags: SwitchTags.Trough);
    }

    private void ConfigureCoils()
    {
        // ── Flippers ──────────────────────────────────────────────────────────
        AddCoil("LeftFlipperMain",  hwNumber: 0x00, defaultPulseMs: 30);
        AddCoil("RightFlipperMain", hwNumber: 0x02, defaultPulseMs: 30);

        // ── Slings ────────────────────────────────────────────────────────────
        AddCoil("LeftSlingCoil",  hwNumber: 0x04, defaultPulseMs: 20);
        AddCoil("RightSlingCoil", hwNumber: 0x05, defaultPulseMs: 20);

        // ── Ball devices ──────────────────────────────────────────────────────
        AddCoil("TroughEject", hwNumber: 0x06, defaultPulseMs: 15);
        AddCoil("AutoLaunch",  hwNumber: 0x07, defaultPulseMs: 70);
    }

    private void ConfigureHardwareRules()
    {
        // Flippers: board fires the coil the instant the button closes;
        // hold power and EOS-based power reduction are managed on-board.
        AddFlipperRule("LeftFlipper",  mainCoil: "LeftFlipperMain",  pulseMs: 30, holdPower: 0.25f);
        AddFlipperRule("RightFlipper", mainCoil: "RightFlipperMain", pulseMs: 30, holdPower: 0.25f);

        // Slings: board fires the coil the instant the switch closes.
        AddBumperRule("LeftSling",  coilName: "LeftSlingCoil",  pulseMs: 20);
        AddBumperRule("RightSling", coilName: "RightSlingCoil", pulseMs: 20);
    }
}
