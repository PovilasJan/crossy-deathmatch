namespace CrossyDeathmatch.Server;

internal static class TaskExtensions
{
    /// <summary>Fire-and-forget a send; a client that vanished mid-send is not the server's problem.</summary>
    public static void Forget(this Task task) =>
        task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
}
