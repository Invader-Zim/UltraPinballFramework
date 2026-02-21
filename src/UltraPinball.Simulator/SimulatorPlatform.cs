using UltraPinball.Core.Devices;
using UltraPinball.Core.Platform;

namespace UltraPinball.Simulator;

/// <summary>
/// A fully in-process hardware platform for development and testing.
/// No physical hardware required.
///
/// Keyboard keys trigger switch events. All coil and LED actions are
/// printed to the console.
///
/// Hardware rules are enforced in simulation:
/// - Flipper rules fire the coil on switch close and disable on open.
/// - Bumper/sling rules pulse the coil on switch close.
/// - Flipper-mapped keys toggle (press to raise, press again to lower)
///   so the flipper stays up while you play, rather than pulsing for 50 ms.
///
/// Usage:
///   var sim = new SimulatorPlatform();
///   sim.MapKey(ConsoleKey.Z, switchHwNumber: 0x00);   // Z key → switch 0
///   sim.SetInitialState(0x10, SwitchState.Closed);      // trough full at startup
/// </summary>
public class SimulatorPlatform : IHardwarePlatform
{
    public event Action<int, SwitchState>? SwitchChanged;

    private readonly Dictionary<ConsoleKey, int> _keyMappings  = new();
    private readonly Dictionary<ConsoleKey, string> _keyLabels  = new();
    private readonly Dictionary<int, SwitchState> _initialStates = new();
    private readonly Dictionary<int, SwitchState> _currentStates = new();
    private CancellationTokenSource? _cts;

    // ── Hardware rule storage ─────────────────────────────────────────────────

    private record FlipperRule(int MainCoilHw, int PulseMs, float HoldPower);
    private record BumperRule(int CoilHw, int PulseMs);

    private readonly Dictionary<int, FlipperRule> _flipperRules = new();
    private readonly Dictionary<int, BumperRule>  _bumperRules  = new();

    // ── Coil log (for test assertions) ───────────────────────────────────────

    /// <summary>
    /// Records every coil action as a short string (e.g. "PULSE 0x00 30ms").
    /// Not cleared automatically — tests should read or clear this as needed.
    /// </summary>
    public List<string> CoilLog { get; } = new();

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Maps a console key to a switch hardware number. Each keypress simulates
    /// a brief contact (close → 50ms → open), like a ball hitting a switch.
    /// Flipper-rule switches are an exception: they toggle instead of pulsing.</summary>
    /// <param name="label">Optional switch name shown in the startup key-map printout.</param>
    public SimulatorPlatform MapKey(ConsoleKey key, int switchHwNumber, string? label = null)
    {
        _keyMappings[key] = switchHwNumber;
        if (label != null) _keyLabels[key] = label;
        return this;
    }

    /// <summary>Sets the initial state of a switch (e.g., trough switches that
    /// start closed because balls are resting on them).</summary>
    public SimulatorPlatform SetInitialState(int hwNumber, SwitchState state)
    {
        _initialStates[hwNumber] = state;
        _currentStates[hwNumber] = state;
        return this;
    }

    // ── IHardwarePlatform ─────────────────────────────────────────────────────

    public Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var inputThread = new Thread(KeyboardLoop)
        {
            IsBackground = true,
            Name = "SimulatorInput"
        };
        inputThread.Start(_cts.Token);

        SimLog("Simulator ready. Press mapped keys to trigger switches. Ctrl+C to quit.");
        PrintKeyMap();
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _cts?.Cancel();
        SimLog("Simulator disconnected.");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<int, SwitchState>> GetInitialSwitchStatesAsync()
    {
        IReadOnlyDictionary<int, SwitchState> result = new Dictionary<int, SwitchState>(_initialStates);
        return Task.FromResult(result);
    }

    // ── Coil control ──────────────────────────────────────────────────────────

    public void PulseCoil(int hwNumber, int milliseconds)
    {
        var entry = $"PULSE 0x{hwNumber:X2} {milliseconds}ms";
        CoilLog.Add(entry);
        SimLog($"[COIL] {entry}");
    }

    public void HoldCoil(int hwNumber)
    {
        var entry = $"HOLD 0x{hwNumber:X2}";
        CoilLog.Add(entry);
        SimLog($"[COIL] {entry}");
    }

    public void DisableCoil(int hwNumber)
    {
        var entry = $"DISABLE 0x{hwNumber:X2}";
        CoilLog.Add(entry);
        SimLog($"[COIL] {entry}");
    }

    // ── Hardware rules ────────────────────────────────────────────────────────

    public void ConfigureFlipperRule(int switchHw, int mainCoilHw, int pulseMs, float holdPower = 0.25f)
    {
        _flipperRules[switchHw] = new FlipperRule(mainCoilHw, pulseMs, holdPower);
        SimLog($"[RULE] Flipper: sw=0x{switchHw:X2} → coil=0x{mainCoilHw:X2} pulse={pulseMs}ms hold={holdPower:P0}");
    }

    public void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs)
    {
        _bumperRules[switchHw] = new BumperRule(coilHw, pulseMs);
        SimLog($"[RULE] Bumper: sw=0x{switchHw:X2} → coil=0x{coilHw:X2} pulse={pulseMs}ms");
    }

    public void RemoveHardwareRule(int switchHw)
    {
        _flipperRules.Remove(switchHw);
        _bumperRules.Remove(switchHw);
        SimLog($"[RULE] Removed rule for sw=0x{switchHw:X2}");
    }

    // ── LEDs ──────────────────────────────────────────────────────────────────

    public void SetLedColor(int hwAddress, byte r, byte g, byte b) =>
        SimLog($"[LED]  0x{hwAddress:X2} → #{r:X2}{g:X2}{b:X2}");

    public void SetLedColors(int startAddress, (byte r, byte g, byte b)[] colors)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            var (r, g, b) = colors[i];
            SimLog($"[LED]  0x{startAddress + i:X2} → #{r:X2}{g:X2}{b:X2}");
        }
    }

    // ── Direct switch triggering (useful in tests) ────────────────────────────

    /// <summary>
    /// Directly sets a switch state without keyboard interaction. Thread-safe.
    /// If a hardware rule is configured for this switch, the corresponding coil
    /// action fires automatically — matching what real hardware would do.
    /// </summary>
    public void TriggerSwitch(int hwNumber, SwitchState state)
    {
        _currentStates[hwNumber] = state;
        SimLog($"[SW]   0x{hwNumber:X2} → {state}");
        SwitchChanged?.Invoke(hwNumber, state);

        // Enforce hardware rules — fire coils exactly as the board would.
        if (_flipperRules.TryGetValue(hwNumber, out var fr))
        {
            if (state == SwitchState.Closed) PulseCoil(fr.MainCoilHw, fr.PulseMs);
            else                             DisableCoil(fr.MainCoilHw);
        }
        else if (_bumperRules.TryGetValue(hwNumber, out var br) && state == SwitchState.Closed)
        {
            PulseCoil(br.CoilHw, br.PulseMs);
        }
    }

    /// <summary>Simulates a momentary switch contact: close then open after a delay.</summary>
    public async Task PulseSwitch(int hwNumber, int contactMs = 50)
    {
        TriggerSwitch(hwNumber, SwitchState.Closed);
        await Task.Delay(contactMs);
        TriggerSwitch(hwNumber, SwitchState.Open);
    }

    // ── Keyboard input loop (background thread) ───────────────────────────────

    private void KeyboardLoop(object? obj)
    {
        var ct = (CancellationToken)(obj ?? CancellationToken.None);

        if (Console.IsInputRedirected)
        {
            SimLog("No console detected — keyboard input disabled.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (_keyMappings.TryGetValue(key.Key, out int hwNum))
                {
                    if (_flipperRules.ContainsKey(hwNum))
                    {
                        // Toggle: press once to raise the flipper, press again to lower.
                        var current = _currentStates.GetValueOrDefault(hwNum, SwitchState.Open);
                        TriggerSwitch(hwNum, current == SwitchState.Open ? SwitchState.Closed : SwitchState.Open);
                    }
                    else
                    {
                        // Momentary: brief contact, like a ball hitting a switch.
                        TriggerSwitch(hwNum, SwitchState.Closed);
                        Thread.Sleep(50);
                        TriggerSwitch(hwNum, SwitchState.Open);
                    }
                }
            }
            Thread.Sleep(5);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SimLog(string message) =>
        Console.WriteLine($"[SIM {DateTime.Now:HH:mm:ss.fff}] {message}");

    private void PrintKeyMap()
    {
        if (_keyMappings.Count == 0) return;
        SimLog("Key mappings:");
        foreach (var (key, hw) in _keyMappings)
        {
            var label = _keyLabels.TryGetValue(key, out var l) ? $" ({l})" : "";
            SimLog($"  {(char)key} → sw 0x{hw:X2}{label}");
        }
    }
}
