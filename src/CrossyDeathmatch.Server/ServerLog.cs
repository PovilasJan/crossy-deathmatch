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

    private ServerLog() { }

    /// <summary>Makes the instance on the first call and returns the same one after that.</summary>
    public static ServerLog GetInstance()
    {
        if (_instance == null)
        {
            lock (InstanceLock)
            {
                // A thread that waited for the lock must not make a second instance.
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
