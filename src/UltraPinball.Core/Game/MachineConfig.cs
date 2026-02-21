using UltraPinball.Core.Devices;
using UltraPinball.Core.Platform;

namespace UltraPinball.Core.Game;

/// <summary>
/// Base class for a machine definition. Subclass this to declare all switches,
/// coils, and LEDs for your specific machine in code — no YAML or config files.
///
/// Example:
///   public class MyMachine : MachineConfig {
///       public override void Configure() {
///           AddSwitch("LeftFlipper",  hwNumber: 0x00, debounce: false);
///           AddCoil("LeftFlipperMain", hwNumber: 0x00, defaultPulseMs: 10);
///           AddFlipperRule("LeftFlipper", mainCoil: "LeftFlipperMain");
///       }
///   }
/// </summary>
public abstract class MachineConfig
{
    public DeviceCollection<Switch> Switches { get; } = new();
    public DeviceCollection<Coil> Coils { get; } = new();
    public DeviceCollection<Led> Leds { get; } = new();

    private IHardwarePlatform? _platform;

    internal void Initialize(IHardwarePlatform platform)
    {
        _platform = platform;
        Configure();
    }

    /// <summary>Override this to declare your machine's hardware.</summary>
    public abstract void Configure();

    // ── Switch registration ───────────────────────────────────────────────────

    /// <summary>Declares a switch and adds it to <see cref="Switches"/>.</summary>
    /// <param name="name">Symbolic name used throughout game code (e.g. <c>"LeftFlipper"</c>).</param>
    /// <param name="hwNumber">Hardware address on the controller board.</param>
    /// <param name="type">Whether the switch is normally-open or normally-closed at rest.</param>
    /// <param name="debounce">
    /// Pass <c>false</c> for flipper buttons and other switches that need the fastest
    /// possible response; hardware debouncing adds a small latency.
    /// </param>
    protected Switch AddSwitch(string name, int hwNumber,
                               SwitchType type = SwitchType.NormallyOpen,
                               bool debounce = true)
    {
        var sw = new Switch(name, hwNumber, type, debounce);
        Switches.Add(name, hwNumber, sw);
        return sw;
    }

    // ── Coil registration ─────────────────────────────────────────────────────

    /// <summary>Declares a solenoid coil and adds it to <see cref="Coils"/>.</summary>
    /// <param name="name">Symbolic name (e.g. <c>"LeftFlipperMain"</c>).</param>
    /// <param name="hwNumber">Hardware address on the controller board.</param>
    /// <param name="defaultPulseMs">
    /// Default pulse duration used when <see cref="Devices.Coil.Pulse"/> is called without
    /// an explicit duration. Tune per coil so it moves the mechanism reliably without
    /// over-stressing the winding.
    /// </param>
    protected Coil AddCoil(string name, int hwNumber, int defaultPulseMs = 20)
    {
        var coil = new Coil(name, hwNumber, defaultPulseMs, Platform);
        Coils.Add(name, hwNumber, coil);
        return coil;
    }

    // ── LED registration ──────────────────────────────────────────────────────

    /// <summary>Declares an addressable RGB LED and adds it to <see cref="Leds"/>.</summary>
    /// <param name="name">Symbolic name (e.g. <c>"ShooterLane"</c>).</param>
    /// <param name="hwAddress">Platform-specific hardware address of the LED.</param>
    protected Led AddLed(string name, int hwAddress)
    {
        var led = new Led(name, hwAddress, Platform);
        Leds.Add(name, hwAddress, led);
        return led;
    }

    // ── Hardware rules ────────────────────────────────────────────────────────

    /// <summary>
    /// Configures a flipper rule on the hardware controller.
    /// The board handles switch → coil without any host round-trip.
    /// After the initial pulse the board holds at <paramref name="holdPower"/> PWM,
    /// with power reduction managed by the EOS switch on-board.
    /// </summary>
    /// <param name="switchName">Name of the flipper button switch as declared with <see cref="AddSwitch"/>.</param>
    /// <param name="mainCoil">Name of the flipper coil.</param>
    /// <param name="pulseMs">Initial power burst duration in milliseconds.</param>
    /// <param name="holdPower">PWM duty cycle (0.0–1.0) applied after the initial pulse to keep the flipper up.</param>
    protected void AddFlipperRule(string switchName, string mainCoil,
                                  int pulseMs = 10, float holdPower = 0.25f)
    {
        var sw   = Switches[switchName];
        var main = Coils[mainCoil];
        Platform.ConfigureFlipperRule(sw.HwNumber, main.HwNumber, pulseMs, holdPower);
    }

    /// <summary>
    /// Removes any hardware rule associated with the named switch.
    /// Use this to temporarily disable flippers (e.g., during tilt or ball save).
    /// </summary>
    /// <param name="switchName">Name of the switch whose rule should be cleared.</param>
    protected void RemoveHardwareRule(string switchName)
    {
        var sw = Switches[switchName];
        Platform.RemoveHardwareRule(sw.HwNumber);
    }

    /// <summary>
    /// Configures a bumper or slingshot rule on the hardware controller.
    /// The board fires the coil the instant the switch closes, with no host round-trip.
    /// </summary>
    /// <param name="switchName">Name of the bumper/slingshot switch.</param>
    /// <param name="coilName">Name of the coil to fire.</param>
    /// <param name="pulseMs">Coil pulse duration in milliseconds.</param>
    protected void AddBumperRule(string switchName, string coilName, int pulseMs = 20)
    {
        var sw = Switches[switchName];
        var coil = Coils[coilName];
        Platform.ConfigureBumperRule(sw.HwNumber, coil.HwNumber, pulseMs);
    }

    private IHardwarePlatform Platform =>
        _platform ?? throw new InvalidOperationException(
            "MachineConfig.Initialize() must be called before Configure().");
}
