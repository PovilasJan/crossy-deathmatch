namespace CrossyDeathmatch.Server;

/// <summary>
/// Singleton: the one server log. The game loop timer and the SignalR hub threads all write to it,
/// so every line goes through the same instance and the same lock and lines never interleave.
/// </summary>
public sealed class ServerLog
{
    private static volatile ServerLog? _instance;
    private static readonly object InstanceLock = new();

    private readonly object _lock = new();

    private ServerLog()
    {
        Write("ServerLog initialized");
    }


    public static ServerLog GetInstance()
    {
        if (_instance == null)
        {
            lock (InstanceLock)
            {
                if (_instance == null) _instance = new ServerLog();
            }
        }
        return _instance;
    }

    public void Info(string text) => Write(text);

    public void Error(Exception ex) => Write($"ERROR {ex}");

    private void Write(string text)
    {
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");
        }
    }
}
