using System.Text.Json.Serialization;
using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CrossyDeathmatch.Bots;

/// <summary>A computer player: readies up, picks a free animal, then hops or casts abilities at random.</summary>
internal sealed class Bot(string name, BotOptions options) : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly TaskCompletionSource _welcomed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<string> _inRoom = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<GameResultDto> _firstGameOver = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private HubConnection? _hub;
    private string? _id;
    private RoomDto? _room;
    private SnapshotDto? _snapshot;
    private int _playingRound;
    private int _snapshotCount;
    private int _errorCount;

    public string Name => name;
    public int SnapshotCount => _snapshotCount;
    public int ErrorCount => _errorCount;
    public Task<GameResultDto> FirstGameOver => _firstGameOver.Task;

    public async Task ConnectAsync(CancellationToken cancellation)
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(options.Url.TrimEnd('/') + HubRoutes.GameHub)
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        hub.On<string, ProfileDto>(nameof(IGameClient.Welcome), (id, _) =>
        {
            _id = id;
            _welcomed.TrySetResult();
        });
        hub.On<RoomDto>(nameof(IGameClient.RoomState), OnRoomState);
        hub.On<SnapshotDto>(nameof(IGameClient.Snapshot), snapshot =>
        {
            Interlocked.Increment(ref _snapshotCount);
            lock (_sync) _snapshot = snapshot;
        });
        hub.On<GameStartDto>(nameof(IGameClient.GameStarted), start =>
        {
            var round = Interlocked.Increment(ref _playingRound);
            _ = Task.Run(() => PlayAsync(round));
        });
        hub.On<GameResultDto>(nameof(IGameClient.GameOver), result =>
        {
            Interlocked.Increment(ref _playingRound);
            _firstGameOver.TrySetResult(result);
        });
        hub.On<string>(nameof(IGameClient.Error), message =>
        {
            Interlocked.Increment(ref _errorCount);
            if (options.Verbose) Console.WriteLine($"[{name}] error: {message}");
        });
        hub.On<string, MessageTone>(nameof(IGameClient.GameEvent), (text, _) =>
        {
            if (options.Verbose) Console.WriteLine($"[{name}] {text}");
        });

        await hub.StartAsync(cancellation);
        _hub = hub;
        await hub.SendAsync(HubMethods.Hello, name, cancellation);
        await _welcomed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
    }

    public async Task<string> CreateRoomAsync(CancellationToken cancellation)
    {
        await _hub!.SendAsync(HubMethods.CreateRoom, true, cancellation);
        return await _inRoom.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
    }

    public async Task JoinRoomAsync(string code, CancellationToken cancellation)
    {
        await _hub!.SendAsync(HubMethods.JoinRoom, code, cancellation);
        await _inRoom.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }

    private void OnRoomState(RoomDto room)
    {
        lock (_sync) _room = room;
        _inRoom.TrySetResult(room.Code);

        var me = room.Players.FirstOrDefault(p => p.Id == _id);
        if (me is null) return;

        switch (room.Phase)
        {
            case RoomPhase.Lobby when !me.Ready:
                Send(HubMethods.SetReady, true);
                break;

            case RoomPhase.CharacterSelect when me.Role == PlayerRole.Runner && me.Character is null:
                var free = CharacterCatalog.All
                    .Select(c => c.Kind)
                    .Where(kind => room.Players.All(p => p.Character != kind))
                    .OrderBy(_ => Random.Shared.Next())
                    .First();
                Send(HubMethods.PickCharacter, free);
                break;

            case RoomPhase.CharacterSelect when !me.Ready:
                Send(HubMethods.SetReady, true);
                break;
        }
    }

    private async Task PlayAsync(int round)
    {
        while (_playingRound == round && _hub?.State == HubConnectionState.Connected)
        {
            RoomDto? room;
            SnapshotDto? snapshot;
            lock (_sync)
            {
                room = _room;
                snapshot = _snapshot;
            }

            var role = room?.Players.FirstOrDefault(p => p.Id == _id)?.Role;
            if (role == PlayerRole.Mastermind)
            {
                if (snapshot is not null) CastSomething(snapshot);
                await Task.Delay(Random.Shared.Next(1200, 2600));
            }
            else
            {
                Send(HubMethods.Move, PickDirection());
                await Task.Delay(Random.Shared.Next(220, 520));
            }
        }
    }

    private void CastSomething(SnapshotDto snapshot)
    {
        var ability = AbilityCatalog.All[Random.Shared.Next(AbilityCatalog.All.Count)];
        if (snapshot.Mastermind is { } me && me.Energy < ability.Cost) return;

        if (ability.Target == AbilityTarget.None)
        {
            Send(HubMethods.UseAbility, ability.Kind, null, null);
            return;
        }

        var lanes = snapshot.Lanes.Where(l => ability.WorksOn(l.Type)).ToList();
        if (lanes.Count == 0) return;
        var lane = lanes[Random.Shared.Next(lanes.Count)];
        Send(HubMethods.UseAbility, ability.Kind, lane.Index, Random.Shared.Next(GameRules.GridWidth));
    }

    private static Direction PickDirection() => Random.Shared.Next(100) switch
    {
        < 55 => Direction.Up,
        < 72 => Direction.Left,
        < 89 => Direction.Right,
        _ => Direction.Down,
    };

    private void Send(string method, params object?[] args)
    {
        if (_hub is not { State: HubConnectionState.Connected } hub) return;
        _ = hub.SendCoreAsync(method, args).ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }
}
