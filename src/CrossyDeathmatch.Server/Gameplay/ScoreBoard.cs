using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

public sealed class ScoreEntry(string id, string name, CharacterKind character, int startLane)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public CharacterKind Character { get; } = character;
    public int Score { get; set; }
    public int Coins { get; set; }
    public int MaxLane { get; set; } = startLane;
}

/// <summary>1 point for every new lane reached, 5 points for every coin.</summary>
public sealed class ScoreBoard
{
    private readonly Dictionary<string, ScoreEntry> _entries = [];

    public void Register(string id, string name, CharacterKind character, int startLane) =>
        _entries[id] = new ScoreEntry(id, name, character, startLane);

    public ScoreEntry Get(string id) => _entries[id];

    public void RecordProgress(string id, int lane)
    {
        var entry = _entries[id];
        if (lane <= entry.MaxLane) return;
        entry.Score += (lane - entry.MaxLane) * GameRules.PointsPerLane;
        entry.MaxLane = lane;
    }

    public void AddCoin(string id)
    {
        var entry = _entries[id];
        entry.Coins++;
        entry.Score += GameRules.PointsPerCoin;
    }

    /// <summary>Survivors first, then by score.</summary>
    public List<RankingEntryDto> Rank(IEnumerable<Runner> runners) => runners
        .Select(r =>
        {
            var entry = _entries[r.Id];
            return new RankingEntryDto(r.Id, r.Name, r.Character, entry.Score, entry.Coins, entry.MaxLane,
                Math.Max(0, r.Lives), r.State != RunnerState.Out);
        })
        .OrderByDescending(e => e.Survived)
        .ThenByDescending(e => e.Score)
        .ToList();
}
