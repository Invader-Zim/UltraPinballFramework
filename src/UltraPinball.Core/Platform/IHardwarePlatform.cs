using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Platform;

/// <summary>
/// The single seam between game logic and physical hardware.
/// All hardware platforms (FAST Neuron, FAST Nano, P-ROC, Simulator) implement this.
/// Game code only ever touches this interface — never platform-specific types.
/// </summary>
public interface IHardwarePlatform
{
    // ── Lifecycle ────────────────────────────────────────────────────────────

    /// <summary>Opens the connection to the hardware controller and performs any required initialisation handshake.</summary>
    /// <param name="ct">Token used to abort the connect sequence.</param>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Gracefully closes the hardware connection and releases any held resources.</summary>
    Task DisconnectAsync();

    // ── Switches ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the physical state of every switch at startup.
    /// Called once by <see cref="UltraPinball.Core.Game.GameController"/> after <see cref="ConnectAsync"/> completes,
    /// so switch objects can be pre-populated before the game loop begins.
    /// </summary>
    Task<IReadOnlyDictionary<int, SwitchState>> GetInitialSwitchStatesAsync();

    /// <summary>
    /// Raised when a switch changes state.
    /// </summary>
    /// <remarks>
    /// This event is typically raised on a background thread (serial read loop, keyboard
    /// listener, etc.). <see cref="UltraPinball.Core.Game.GameController"/> enqueues events into a
    /// <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> and processes them
    /// on the main game-loop thread — platform implementations must not assume the handler
    /// runs on any particular thread.
    /// </remarks>
    event Action<int, SwitchState>? SwitchChanged; // (hwNumber, newState)

    // ── Coils / Drivers ───────────────────────────────────────────────────────

    /// <summary>Fires a coil for the specified duration, then cuts power.</summary>
    /// <param name="hwNumber">Hardware address of the driver.</param>
    /// <param name="milliseconds">How long to energise the coil.</param>
    void PulseCoil(int hwNumber, int milliseconds);

    /// <summary>
    /// Holds a coil on at full power indefinitely.
    /// Always call <see cref="DisableCoil"/> when done to avoid burning out the winding.
    /// </summary>
    /// <param name="hwNumber">Hardware address of the driver.</param>
    void HoldCoil(int hwNumber);

    /// <summary>Cuts power to a coil immediately.</summary>
    /// <param name="hwNumber">Hardware address of the driver.</param>
    void DisableCoil(int hwNumber);

    // ── Hardware Rules (execute on-board, zero host latency) ─────────────────

    /// <summary>
    /// Configures a flipper rule directly on the hardware controller.
    /// The board fires the coil without waiting for host software — critical for
    /// the ~1ms latency requirement of flippers.
    /// After the initial pulse the board holds the coil at <paramref name="holdPower"/> PWM;
    /// the EOS switch tells the board when to reduce power. This is entirely board-managed.
    /// </summary>
    /// <param name="switchHw">Hardware number of the flipper button switch.</param>
    /// <param name="mainCoilHw">Hardware number of the flipper coil.</param>
    /// <param name="pulseMs">Initial power burst duration in milliseconds.</param>
    /// <param name="holdPower">PWM duty cycle (0.0–1.0) for the hold phase after the pulse.</param>
    void ConfigureFlipperRule(int switchHw, int mainCoilHw,
                              int pulseMs, float holdPower = 0.25f);

    /// <summary>
    /// Configures a bumper or slingshot rule on the hardware controller.
    /// The board fires the coil the instant the switch closes, with no host round-trip.
    /// </summary>
    /// <param name="switchHw">Hardware number of the bumper/slingshot switch.</param>
    /// <param name="coilHw">Hardware number of the coil to fire.</param>
    /// <param name="pulseMs">Coil pulse duration in milliseconds.</param>
    void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs);

    /// <summary>Removes any hardware rule associated with the given switch.</summary>
    /// <param name="switchHw">Hardware number of the switch whose rule should be cleared.</param>
    void RemoveHardwareRule(int switchHw);

    // ── LEDs ─────────────────────────────────────────────────────────────────

    /// <summary>Sets a single LED to the given RGB colour.</summary>
    /// <param name="hwAddress">Platform-specific hardware address of the LED.</param>
    /// <param name="r">Red component (0–255).</param>
    /// <param name="g">Green component (0–255).</param>
    /// <param name="b">Blue component (0–255).</param>
    void SetLedColor(int hwAddress, byte r, byte g, byte b);

    /// <summary>
    /// Sets a contiguous run of LEDs starting at <paramref name="startAddress"/>.
    /// Platforms that support bulk updates can implement this more efficiently than
    /// repeated <see cref="SetLedColor"/> calls.
    /// </summary>
    /// <param name="startAddress">Hardware address of the first LED in the run.</param>
    /// <param name="colors">RGB values for each LED; <c>colors[0]</c> maps to <paramref name="startAddress"/>.</param>
    void SetLedColors(int startAddress, (byte r, byte g, byte b)[] colors);
}
