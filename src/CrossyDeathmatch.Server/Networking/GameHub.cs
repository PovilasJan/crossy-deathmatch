using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR;

namespace CrossyDeathmatch.Server.Networking;

/// <summary>The SignalR endpoint. It only translates calls; all decisions live in <see cref="GameServer"/>.</summary>
public sealed class GameHub(GameServer server) : Hub<IGameClient>
{
    public void Hello(string name) => server.Hello(Context.ConnectionId, name);

    public void ListRooms() => server.ListRooms(Context.ConnectionId);

    public void CreateRoom(bool isPublic) => server.CreateRoom(Context.ConnectionId, isPublic);

    public void JoinRoom(string code) => server.JoinRoom(Context.ConnectionId, code);

    public void LeaveRoom() => server.LeaveRoom(Context.ConnectionId);

    public void SetVisibility(bool isPublic) =>
        server.InSession(Context.ConnectionId, (session, player) => session.SetVisibility(player, isPublic));

    public void SetReady(bool ready) =>
        server.InSession(Context.ConnectionId, (session, player) => session.SetReady(player, ready));

    public void PickCharacter(CharacterKind character) =>
        server.InSession(Context.ConnectionId, (session, player) => session.PickCharacter(player, character));

    public void Move(Direction direction) =>
        server.InSession(Context.ConnectionId, (session, player) => session.Move(player, direction));

    public void UseAbility(AbilityKind ability, int? lane, int? column) =>
        server.InSession(Context.ConnectionId, (session, player) => session.UseAbility(player, ability, lane, column));

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        server.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
