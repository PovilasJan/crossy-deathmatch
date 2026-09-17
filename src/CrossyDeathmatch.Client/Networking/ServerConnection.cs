using System.Text.Json.Serialization;
using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CrossyDeathmatch.Client.Networking;

/// <summary>
/// Wraps the SignalR connection. Events fire on background threads; the UI marshals them itself.
/// </summary>
public sealed class ServerConnection : IAsyncDisposable
{
    private HubConnection? _hub;

    public event Action<string, ProfileDto>? Welcome;
    public event Action<ProfileDto>? ProfileUpdated;
    public event Action<string>? Error;
    public event Action<List<RoomSummaryDto>>? RoomList;
    public event Action<RoomDto>? RoomState;
    public event Action? LeftRoom;
    public event Action<GameStartDto>? GameStarted;
    public event Action<SnapshotDto>? Snapshot;
    public event Action<string, MessageTone>? GameEvent;
    public event Action<GameResultDto>? GameOver;
    public event Action<string?>? Closed;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(string serverUrl)
    {
        await DisposeAsync();

        var hub = new HubConnectionBuilder()
            .WithUrl(serverUrl.TrimEnd('/') + HubRoutes.GameHub)
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        hub.On<string, ProfileDto>(nameof(IGameClient.Welcome), (id, profile) => Welcome?.Invoke(id, profile));
        hub.On<ProfileDto>(nameof(IGameClient.Profile), profile => ProfileUpdated?.Invoke(profile));
        hub.On<string>(nameof(IGameClient.Error), message => Error?.Invoke(message));
        hub.On<List<RoomSummaryDto>>(nameof(IGameClient.RoomList), rooms => RoomList?.Invoke(rooms));
        hub.On<RoomDto>(nameof(IGameClient.RoomState), room => RoomState?.Invoke(room));
        hub.On(nameof(IGameClient.LeftRoom), () => LeftRoom?.Invoke());
        hub.On<GameStartDto>(nameof(IGameClient.GameStarted), start => GameStarted?.Invoke(start));
        hub.On<SnapshotDto>(nameof(IGameClient.Snapshot), snapshot => Snapshot?.Invoke(snapshot));
        hub.On<string, MessageTone>(nameof(IGameClient.GameEvent), (text, tone) => GameEvent?.Invoke(text, tone));
        hub.On<GameResultDto>(nameof(IGameClient.GameOver), result => GameOver?.Invoke(result));
        hub.Closed += exception =>
        {
            Closed?.Invoke(exception?.Message);
            return Task.CompletedTask;
        };

        await hub.StartAsync();
        _hub = hub;
    }

    public Task Hello(string name) => Send(HubMethods.Hello, name);
    public Task ListRooms() => Send(HubMethods.ListRooms);
    public Task CreateRoom(bool isPublic) => Send(HubMethods.CreateRoom, isPublic);
    public Task JoinRoom(string code) => Send(HubMethods.JoinRoom, code);
    public Task LeaveRoom() => Send(HubMethods.LeaveRoom);
    public Task SetVisibility(bool isPublic) => Send(HubMethods.SetVisibility, isPublic);
    public Task SetReady(bool ready) => Send(HubMethods.SetReady, ready);
    public Task PickCharacter(CharacterKind character) => Send(HubMethods.PickCharacter, character);
    public Task Move(Direction direction) => Send(HubMethods.Move, direction);
    public Task UseAbility(AbilityKind ability, int? lane, int? column) => Send(HubMethods.UseAbility, ability, lane, column);

    public async ValueTask DisposeAsync()
    {
        var hub = _hub;
        _hub = null;
        if (hub is not null) await hub.DisposeAsync();
    }

    private async Task Send(string method, params object?[] args)
    {
        if (_hub is not { State: HubConnectionState.Connected } hub) return;
        try
        {
            await hub.SendCoreAsync(method, args);
        }
        catch (Exception)
        {
            // The connection is going away; Closed will tell the UI.
        }
    }
}
