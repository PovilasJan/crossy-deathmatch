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

    /// <summary>Private, so only <see cref="GetInstance"/> can make the instance. It logs itself, so the log shows it ran once.</summary>
    private ServerLog()
    {
        Write("ServerLog initialized");
    }

    /// <summary>
    /// Makes the instance on the first call and returns the same one after that. Thread safe: the lock lets one
    /// thread at a time make the instance, and the second null check stops a thread that waited for the lock from
    /// making another one. <see cref="SingletonDemo"/> shows it at every server start.
    /// </summary>
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
