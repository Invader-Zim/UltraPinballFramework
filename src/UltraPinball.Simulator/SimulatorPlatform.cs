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
/// Usage:
///   var sim = new SimulatorPlatform();
///   sim.MapKey(ConsoleKey.Z, switchHwNumber: 0x00);   // Z key → switch 0
///   sim.SetInitialState(0x10, SwitchState.Closed);      // trough full at startup
/// </summary>
public class SimulatorPlatform : IHardwarePlatform
{
    public event Action<int, SwitchState>? SwitchChanged;

    private readonly Dictionary<ConsoleKey, int> _keyMappings = new();
    private readonly Dictionary<int, SwitchState> _initialStates = new();
    private readonly Dictionary<int, SwitchState> _currentStates = new();
    private CancellationTokenSource? _cts;

    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Maps a console key to a switch hardware number. Each keypress simulates
    /// a brief contact (close → 50ms → open), like a ball hitting a switch.</summary>
    public SimulatorPlatform MapKey(ConsoleKey key, int switchHwNumber)
    {
        _keyMappings[key] = switchHwNumber;
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

    public void PulseCoil(int hwNumber, int milliseconds) =>
        SimLog($"[COIL] 0x{hwNumber:X2} PULSE {milliseconds}ms");

    public void HoldCoil(int hwNumber) =>
        SimLog($"[COIL] 0x{hwNumber:X2} HOLD");

    public void DisableCoil(int hwNumber) =>
        SimLog($"[COIL] 0x{hwNumber:X2} DISABLE");

    // ── Hardware rules ────────────────────────────────────────────────────────

    public void ConfigureFlipperRule(int switchHw, int mainCoilHw, int? holdCoilHw,
                                     int pulseMs, float holdPower = 0.25f) =>
        SimLog($"[RULE] Flipper: sw=0x{switchHw:X2} → main=0x{mainCoilHw:X2}" +
               (holdCoilHw.HasValue ? $" hold=0x{holdCoilHw.Value:X2}" : "") +
               $" pulse={pulseMs}ms hold={holdPower:P0}");

    public void ConfigureBumperRule(int switchHw, int coilHw, int pulseMs) =>
        SimLog($"[RULE] Bumper: sw=0x{switchHw:X2} → coil=0x{coilHw:X2} pulse={pulseMs}ms");

    public void RemoveHardwareRule(int switchHw) =>
        SimLog($"[RULE] Removed rule for sw=0x{switchHw:X2}");

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

    /// <summary>Directly sets a switch state without keyboard interaction. Thread-safe.</summary>
    public void TriggerSwitch(int hwNumber, SwitchState state)
    {
        _currentStates[hwNumber] = state;
        SimLog($"[SW]   0x{hwNumber:X2} → {state}");
        SwitchChanged?.Invoke(hwNumber, state);
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
                    TriggerSwitch(hwNum, SwitchState.Closed);
                    Thread.Sleep(50); // simulate contact time
                    TriggerSwitch(hwNum, SwitchState.Open);
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
            SimLog($"  {key} → sw 0x{hw:X2}");
    }
}
