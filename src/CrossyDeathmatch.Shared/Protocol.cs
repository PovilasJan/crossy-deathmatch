namespace CrossyDeathmatch.Shared;

public static class HubRoutes
{
    public const string GameHub = "/game";
}

/// <summary>Hub methods a client may call on the server.</summary>
public static class HubMethods
{
    public const string Hello = nameof(Hello);
    public const string ListRooms = nameof(ListRooms);
    public const string CreateRoom = nameof(CreateRoom);
    public const string JoinRoom = nameof(JoinRoom);
    public const string LeaveRoom = nameof(LeaveRoom);
    public const string SetVisibility = nameof(SetVisibility);
    public const string SetReady = nameof(SetReady);
    public const string PickCharacter = nameof(PickCharacter);
    public const string Move = nameof(Move);
    public const string UseAbility = nameof(UseAbility);
}

/// <summary>Messages the server pushes to a client.</summary>
public interface IGameClient
{
    Task Welcome(string playerId, ProfileDto profile);
    Task Profile(ProfileDto profile);
    Task Error(string message);
    Task RoomList(List<RoomSummaryDto> rooms);
    Task RoomState(RoomDto room);
    Task LeftRoom();
    Task GameStarted(GameStartDto start);
    Task Snapshot(SnapshotDto snapshot);
    Task GameEvent(string text, MessageTone tone);
    Task GameOver(GameResultDto result);
}
