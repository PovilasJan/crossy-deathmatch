using System.Text.Json;
using System.Text.Json.Serialization;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Persistence;

public sealed record MatchRecord(
    DateTime PlayedAtUtc,
    PlayerRole WinnerRole,
    string WinnerName,
    string Reason,
    string? MastermindName,
    List<RankingEntryDto> Ranking);

/// <summary>
/// Tiny file-backed store for player profiles (coins, games, wins, best score) and match history.
/// A real database can replace it later without touching the game code.
/// </summary>
public sealed class JsonDatabase
{
    private const int MatchHistoryLimit = 200;

    private sealed class FileContents
    {
        public List<ProfileDto> Profiles { get; set; } = [];
        public List<MatchRecord> Matches { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _sync = new();
    private readonly string _path;
    private readonly Dictionary<string, ProfileDto> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MatchRecord> _matches = [];

    public JsonDatabase(string path)
    {
        _path = path;
        if (!File.Exists(path)) return;

        var contents = JsonSerializer.Deserialize<FileContents>(File.ReadAllText(path), JsonOptions) ?? new FileContents();
        foreach (var profile in contents.Profiles) _profiles[profile.Name] = profile;
        _matches.AddRange(contents.Matches);
    }

    public ProfileDto GetProfile(string name)
    {
        lock (_sync)
        {
            if (_profiles.TryGetValue(name, out var existing)) return existing;
            var created = new ProfileDto(name, 0, 0, 0, 0);
            _profiles[name] = created;
            Save();
            return created;
        }
    }

    public void RecordMatch(GameResultDto result)
    {
        lock (_sync)
        {
            foreach (var entry in result.Ranking)
            {
                var profile = _profiles.GetValueOrDefault(entry.Name) ?? new ProfileDto(entry.Name, 0, 0, 0, 0);
                _profiles[entry.Name] = profile with
                {
                    Coins = profile.Coins + entry.Coins,
                    GamesPlayed = profile.GamesPlayed + 1,
                    Wins = profile.Wins + (result.WinnerId == entry.Id ? 1 : 0),
                    BestScore = Math.Max(profile.BestScore, entry.Score),
                };
            }

            if (result.MastermindName is { } mastermind)
            {
                var profile = _profiles.GetValueOrDefault(mastermind) ?? new ProfileDto(mastermind, 0, 0, 0, 0);
                _profiles[mastermind] = profile with
                {
                    GamesPlayed = profile.GamesPlayed + 1,
                    Wins = profile.Wins + (result.WinnerRole == PlayerRole.Mastermind ? 1 : 0),
                };
            }

            _matches.Add(new MatchRecord(DateTime.UtcNow, result.WinnerRole, result.WinnerName, result.Reason, result.MastermindName, result.Ranking));
            if (_matches.Count > MatchHistoryLimit) _matches.RemoveRange(0, _matches.Count - MatchHistoryLimit);
            Save();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var contents = new FileContents { Profiles = [.. _profiles.Values], Matches = _matches };
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(contents, JsonOptions));
        File.Move(temp, _path, overwrite: true);
    }
}
