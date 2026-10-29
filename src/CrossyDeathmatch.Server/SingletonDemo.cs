namespace CrossyDeathmatch.Server;

/// <summary>
/// Shows that the <see cref="ServerLog"/> singleton is thread safe. Program runs it at every server start, before
/// anything else has asked for the log: several threads are released at the same moment and all call
/// <see cref="ServerLog.GetInstance"/> while the instance does not exist yet. The log then shows
/// "ServerLog initialized" once, and every thread got the same object.
/// </summary>
public static class SingletonDemo
{
    private const int Threads = 8;

    public static void Run()
    {
        var instances = new ServerLog[Threads];
        using var start = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads)
            .Select(i => new Thread(() =>
            {
                start.SignalAndWait(); // every thread waits here until all of them are ready, then they all go at once
                instances[i] = ServerLog.GetInstance();
            }))
            .ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var distinct = instances.Distinct(ReferenceEqualityComparer.Instance).Count();
        ServerLog.GetInstance().Info(
            $"Singleton check: {Threads} threads called GetInstance() at the same time and got {distinct} instance{(distinct == 1 ? "" : "s")}");
    }
}
