using System.Collections;

namespace UltraPinball.Core.Devices;

/// <summary>
/// Named, iterable collection of hardware devices (switches, coils, LEDs).
/// Lookup by symbolic name or hardware number.
/// </summary>
public class DeviceCollection<T> : IEnumerable<T> where T : class
{
    private readonly Dictionary<string, T> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, T> _byHwNumber = new();

    internal void Add(string name, int hwNumber, T device)
    {
        _byName[name] = device;
        _byHwNumber[hwNumber] = device;
    }

    public T this[string name] => _byName.TryGetValue(name, out var d) ? d
        : throw new KeyNotFoundException($"No device named '{name}'.");

    public T this[int hwNumber] => _byHwNumber.TryGetValue(hwNumber, out var d) ? d
        : throw new KeyNotFoundException($"No device with hw number 0x{hwNumber:X2}.");

    public bool Contains(string name) => _byName.ContainsKey(name);

    public bool TryGet(string name, out T? device) => _byName.TryGetValue(name, out device);

    public bool TryGetByHw(int hwNumber, out T? device) =>
        _byHwNumber.TryGetValue(hwNumber, out device);

    public IEnumerator<T> GetEnumerator() => _byName.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
