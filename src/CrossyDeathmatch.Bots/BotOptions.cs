namespace CrossyDeathmatch.Bots;

internal sealed record BotOptions(string Url, int Count, string? Room, bool ExitAfterGame, bool Verbose)
{
    public static BotOptions Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        return new BotOptions(
            Value("--url") ?? "http://localhost:5000",
            Math.Clamp(int.TryParse(Value("--count"), out var count) ? count : 3, 1, 4),
            Value("--room")?.ToUpperInvariant(),
            args.Contains("--exit-after-game"),
            args.Contains("--verbose"));
    }
}
