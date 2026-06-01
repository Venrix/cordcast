using System.Text.Json;

namespace CordCastWorker;

/// <summary>
/// Helpers for reading commands from stdin and writing events to stdout (newline-delimited JSON).
/// </summary>
public static class Ipc
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void Emit(string eventName, object? payload = null)
    {
        var obj = new Dictionary<string, object?> { ["event"] = eventName };
        if (payload is Dictionary<string, object?> dict)
            foreach (var kv in dict) obj[kv.Key] = kv.Value;

        Console.WriteLine(JsonSerializer.Serialize(obj));
    }

    public static async IAsyncEnumerable<Dictionary<string, JsonElement>> ReadCommands(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await Console.In.ReadLineAsync(ct);
            if (line is null) yield break;
            line = line.Trim();
            if (line.Length == 0) continue;

            Dictionary<string, JsonElement>? cmd = null;
            try
            {
                cmd = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line, _opts);
            }
            catch { }

            if (cmd is not null) yield return cmd;
        }
    }

    public static string? GetString(this Dictionary<string, JsonElement> cmd, string key) =>
        cmd.TryGetValue(key, out var el) ? el.GetString() : null;

    public static bool GetBool(this Dictionary<string, JsonElement> cmd, string key,
        bool defaultValue = false) =>
        cmd.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.True
            ? true
            : cmd.TryGetValue(key, out el) && el.ValueKind == JsonValueKind.False
                ? false
                : defaultValue;

    public static double GetDouble(this Dictionary<string, JsonElement> cmd, string key,
        double defaultValue = 0) =>
        cmd.TryGetValue(key, out var el) && el.TryGetDouble(out var v) ? v : defaultValue;

    public static T? Deserialize<T>(this Dictionary<string, JsonElement> cmd, string key)
    {
        if (!cmd.TryGetValue(key, out var el)) return default;
        try { return el.Deserialize<T>(); } catch { return default; }
    }
}
