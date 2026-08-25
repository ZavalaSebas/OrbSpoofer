using System.IO;
using System.Text.Json;

namespace OrbSpoofer.Infrastructure.Settings;

public abstract class SettingsStoreBase<T>
{
    protected abstract string CurrentFilePath { get; }
    protected virtual string? LegacyFilePath => null;
    protected abstract T DefaultValue { get; }

    public T Load()
    {
        var current = TryLoadFrom(CurrentFilePath);
        if (current.success) return current.value!;
        if (!string.IsNullOrWhiteSpace(LegacyFilePath))
        {
            var legacy = TryLoadFrom(LegacyFilePath!);
            if (legacy.success) return legacy.value!;
        }
        return DefaultValue;
    }

    public void Save(T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(CurrentFilePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(CurrentFilePath, Serialize(value));
        }
        catch { }
    }

    protected virtual string Serialize(T value) => JsonSerializer.Serialize(value);
    protected virtual bool TryParse(string raw, out T value)
    {
        try { value = JsonSerializer.Deserialize<T>(raw)!; return value is not null; }
        catch { value = DefaultValue; return false; }
    }

    private (bool success, T? value) TryLoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return (false, default);
            var raw = File.ReadAllText(path);
            if (TryParse(raw, out var value)) return (true, value);
        }
        catch { }
        return (false, default);
    }
}
