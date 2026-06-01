namespace CordCastWorker;

public static class Logger
{
    private static StreamWriter? _writer;
    private static readonly object _lock = new();

    public static void Init(string path)
    {
        _writer = new StreamWriter(path, append: false, encoding: System.Text.Encoding.UTF8)
        {
            AutoFlush = true,
        };
        Write("INFO", "Logger", $"Log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    public static void Write(string severity, string source, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{severity,-7}] [{source}] {message}";
        lock (_lock)
        {
            _writer?.WriteLine(line);
        }
    }

    public static void Close() => _writer?.Dispose();
}
