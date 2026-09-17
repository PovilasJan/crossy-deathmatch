using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Read from the command line, e.g. <c>dotnet run -- --port 5000 --min-players 2 --round-seconds 60</c>.
/// </summary>
public sealed record ServerSettings(int Port, int MinPlayers, int RoundSeconds)
{
    public static ServerSettings From(IConfiguration configuration) => new(
        configuration.GetValue("port", 5000),
        Math.Clamp(configuration.GetValue("min-players", GameRules.MaxPlayers), 1, GameRules.MaxPlayers),
        Math.Max(10, configuration.GetValue("round-seconds", GameRules.DefaultRoundSeconds)));
}
