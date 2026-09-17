namespace CrossyDeathmatch.Client;

internal static class Program
{
    /// <summary>
    /// Optional arguments: <c>--server http://192.168.1.20:5000</c> and <c>--name Povilas</c>.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var server = Argument(args, "--server") ?? "http://localhost:5000";
        var name = Argument(args, "--name");
        Application.Run(new MainForm(server, name));
    }

    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
