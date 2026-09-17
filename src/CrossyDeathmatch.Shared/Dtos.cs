namespace CrossyDeathmatch.Shared;

// Everything that travels between server and client. SignalR serialises these records as JSON.

public sealed record ProfileDto(string Name, int Coins, int GamesPlayed, int Wins, int BestScore);

public sealed record RoomPlayerDto(
    string Id,
    string Name,
    bool Ready,
    PlayerRole? Role,
    CharacterKind? Character,
    bool IsHost);

public sealed record RoomDto(
    string Code,
    bool IsPublic,
    RoomPhase Phase,
    string HostId,
    int MinPlayers,
    int MaxPlayers,
    List<RoomPlayerDto> Players);

public sealed record RoomSummaryDto(string Code, string HostName, int PlayerCount, int MaxPlayers, RoomPhase Phase);

public sealed record GameStartDto(int GridWidth, int DurationMs);

public sealed record PickupDto(int X, PickupKind Kind);

public sealed record LaneDto(int Index, LaneType Type, List<int> Trees, List<PickupDto> Pickups, bool Warning);

/// <summary>X is the left edge in tiles; Heading is +1 (moving right), -1 (moving left) or 0.</summary>
public sealed record ObstacleDto(int Id, ObstacleKind Kind, int Lane, float X, int Length, int Heading, bool Landed);

public sealed record RunnerDto(
    string Id,
    string Name,
    CharacterKind Character,
    float X,
    float Lane,
    int Lives,
    int Score,
    int Coins,
    RunnerState State,
    bool Invulnerable,
    bool Boosted,
    bool Connected);

public sealed record MastermindDto(
    string Id,
    string Name,
    float Energy,
    float MaxEnergy,
    Dictionary<AbilityKind, int> CooldownsMs,
    bool Connected);

public sealed record SnapshotDto(
    long ServerTimeMs,
    int TimeLeftMs,
    List<LaneDto> Lanes,
    List<ObstacleDto> Obstacles,
    List<RunnerDto> Runners,
    MastermindDto? Mastermind,
    int RainMs,
    int NightMs);

public sealed record RankingEntryDto(
    string Id,
    string Name,
    CharacterKind Character,
    int Score,
    int Coins,
    int MaxLane,
    int LivesLeft,
    bool Survived);

public sealed record GameResultDto(
    PlayerRole WinnerRole,
    string? WinnerId,
    string WinnerName,
    string Reason,
    List<RankingEntryDto> Ranking,
    string? MastermindName);
