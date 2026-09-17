using CrossyDeathmatch.Server.Gameplay;
using CrossyDeathmatch.Server.Persistence;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Networking;

/// <summary>
/// A room: up to four players moving through Lobby → CharacterSelect → Playing → Results → Lobby.
/// Everything inside is guarded by one lock, which the game loop also takes on every tick.
/// </summary>
public sealed class GameSession : IRoundEvents, IDisposable
{
    private sealed class Member(PlayerConnection connection)
    {
        public PlayerConnection Connection { get; } = connection;
        public bool Ready { get; set; }
        public PlayerRole? Role { get; set; }
        public CharacterKind? Character { get; set; }
    }

    private readonly object _sync = new();
    private readonly List<Member> _members = [];
    private readonly ServerSettings _settings;
    private readonly JsonDatabase _database;
    private readonly Action _roomListChanged;
    private readonly ILogger<GameSession> _log;
    private RoomPhase _phase = RoomPhase.Lobby;
    private bool _isPublic;
    private string _hostId = "";
    private GameRound? _round;
    private CancellationTokenSource? _loop;
    private int _generation;

    public GameSession(string code, bool isPublic, ServerSettings settings, JsonDatabase database, Action roomListChanged, ILogger<GameSession> log)
    {
        Code = code;
        _isPublic = isPublic;
        _settings = settings;
        _database = database;
        _roomListChanged = roomListChanged;
        _log = log;
    }

    public string Code { get; }

    public bool IsPublic
    {
        get { lock (_sync) return _isPublic; }
    }

    public bool IsEmpty
    {
        get { lock (_sync) return _members.Count == 0; }
    }

    public string? JoinBlocker()
    {
        lock (_sync)
        {
            if (_phase != RoomPhase.Lobby) return "That room is already playing";
            if (_members.Count >= GameRules.MaxPlayers) return "That room is full";
            return null;
        }
    }

    public void Join(PlayerConnection player)
    {
        lock (_sync)
        {
            _members.Add(new Member(player));
            if (_hostId == "") _hostId = player.Id;
            Announce($"{player.Name} joined the room", MessageTone.Info);
            PublishState();
        }
    }

    public void Leave(PlayerConnection player)
    {
        lock (_sync)
        {
            var member = Find(player);
            if (member is null) return;

            _members.Remove(member);
            if (_hostId == player.Id) _hostId = _members.FirstOrDefault()?.Connection.Id ?? "";

            switch (_phase)
            {
                case RoomPhase.CharacterSelect:
                    ReturnToLobby($"{player.Name} left, so the room went back to the lobby");
                    return;
                case RoomPhase.Playing when _members.Count == 0:
                    StopRound();
                    break;
                case RoomPhase.Playing:
                    _round?.RemovePlayer(player.Id);
                    break;
                default:
                    Announce($"{player.Name} left the room", MessageTone.Info);
                    break;
            }
            PublishState();
        }
    }

    public void SetVisibility(PlayerConnection player, bool isPublic)
    {
        lock (_sync)
        {
            if (player.Id != _hostId)
            {
                player.Client.Error("Only the host can change that").Forget();
                return;
            }
            _isPublic = isPublic;
            PublishState();
            _roomListChanged();
        }
    }

    public void SetReady(PlayerConnection player, bool ready)
    {
        lock (_sync)
        {
            var member = Find(player);
            if (member is null) return;

            switch (_phase)
            {
                case RoomPhase.Lobby:
                    member.Ready = ready;
                    PublishState();
                    if (_members.Count >= _settings.MinPlayers && _members.All(m => m.Ready)) AssignRoles();
                    break;

                case RoomPhase.CharacterSelect:
                    if (ready && member.Role == PlayerRole.Runner && member.Character is null)
                    {
                        player.Client.Error("Pick an animal first").Forget();
                        return;
                    }
                    member.Ready = ready;
                    PublishState();
                    if (_members.All(m => m.Ready)) StartRound();
                    break;

                // A late click from the previous phase; nothing to do.
            }
        }
    }

    public void PickCharacter(PlayerConnection player, CharacterKind character)
    {
        lock (_sync)
        {
            var member = Find(player);
            if (member is null) return;
            if (_phase != RoomPhase.CharacterSelect || member.Role != PlayerRole.Runner)
            {
                player.Client.Error("You can't pick an animal right now").Forget();
                return;
            }
            if (_members.Any(m => m != member && m.Character == character))
            {
                player.Client.Error($"Someone already picked the {CharacterCatalog.Get(character).Name}").Forget();
                return;
            }

            member.Character = character;
            member.Ready = false;
            PublishState();
        }
    }

    public void Move(PlayerConnection player, Direction direction)
    {
        lock (_sync) _round?.Move(player.Id, direction, Environment.TickCount64);
    }

    public void UseAbility(PlayerConnection player, AbilityKind ability, int? lane, int? column)
    {
        lock (_sync)
        {
            if (_round?.UseAbility(player.Id, ability, lane, column, Environment.TickCount64) is { } error)
            {
                player.Client.Error(error).Forget();
            }
        }
    }

    public RoomSummaryDto ToSummary()
    {
        lock (_sync)
        {
            var host = _members.FirstOrDefault(m => m.Connection.Id == _hostId)?.Connection.Name ?? "?";
            return new RoomSummaryDto(Code, host, _members.Count, GameRules.MaxPlayers, _phase);
        }
    }

    public void Dispose()
    {
        lock (_sync) StopRound();
    }

    // ---- IRoundEvents: called from inside Tick, so the lock is already held ----

    void IRoundEvents.OnSnapshot(SnapshotDto snapshot)
    {
        foreach (var member in _members) member.Connection.Client.Snapshot(snapshot).Forget();
    }

    void IRoundEvents.OnAnnouncement(string text, MessageTone tone) => Announce(text, tone);

    void IRoundEvents.OnFinished(GameResultDto result)
    {
        StopRound();
        _phase = RoomPhase.Results;
        foreach (var member in _members) member.Connection.Client.GameOver(result).Forget();

        _database.RecordMatch(result);
        foreach (var member in _members)
        {
            member.Connection.Client.Profile(_database.GetProfile(member.Connection.Name)).Forget();
        }

        PublishState();
        _roomListChanged();
        _log.LogInformation("Room {Code} round finished: {Winner} ({Role}) — {Reason}", Code, result.WinnerName, result.WinnerRole, result.Reason);

        var generation = _generation;
        _ = Task.Delay(GameRules.ResultsScreenMs).ContinueWith(_ =>
        {
            lock (_sync)
            {
                if (_generation == generation && _phase == RoomPhase.Results) ReturnToLobby(null);
            }
        });
    }

    // ---- private helpers, all called with the lock held ----

    private Member? Find(PlayerConnection player) => _members.FirstOrDefault(m => m.Connection == player);

    private void AssignRoles()
    {
        _phase = RoomPhase.CharacterSelect;
        var mastermindIndex = _members.Count >= 2 ? Random.Shared.Next(_members.Count) : -1;
        for (var i = 0; i < _members.Count; i++)
        {
            _members[i].Ready = false;
            _members[i].Character = null;
            _members[i].Role = i == mastermindIndex ? PlayerRole.Mastermind : PlayerRole.Runner;
        }

        if (mastermindIndex >= 0) Announce($"{_members[mastermindIndex].Connection.Name} is the Mastermind!", MessageTone.Bad);
        PublishState();
        _roomListChanged();
    }

    private void StartRound()
    {
        var runners = _members
            .Where(m => m.Role == PlayerRole.Runner)
            .Select(m => new RoundParticipant(m.Connection.Id, m.Connection.Name, m.Character ?? CharacterKind.Chicken))
            .ToList();
        var mastermind = _members.FirstOrDefault(m => m.Role == PlayerRole.Mastermind);
        var durationMs = _settings.RoundSeconds * 1000;

        var round = new GameRound(
            runners,
            mastermind is null ? null : new RoundParticipant(mastermind.Connection.Id, mastermind.Connection.Name, default),
            durationMs,
            this,
            Environment.TickCount64);

        _round = round;
        _phase = RoomPhase.Playing;
        foreach (var member in _members) member.Connection.Client.GameStarted(new GameStartDto(GameRules.GridWidth, durationMs)).Forget();
        PublishState();
        _roomListChanged();
        StartLoop(round);
        _log.LogInformation("Room {Code} round started with {Runners} runner(s)", Code, runners.Count);
    }

    private void StartLoop(GameRound round)
    {
        var loop = new CancellationTokenSource();
        _loop = loop;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000.0 / GameRules.TickRate));
            try
            {
                while (await timer.WaitForNextTickAsync(loop.Token))
                {
                    lock (_sync)
                    {
                        if (loop.IsCancellationRequested || _round != round) return;
                        try
                        {
                            round.Tick(Environment.TickCount64);
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Room {Code} tick failed", Code);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Round stopped.
            }
        });
    }

    private void StopRound()
    {
        _loop?.Cancel();
        _loop = null;
        _round = null;
        _generation++;
    }

    private void ReturnToLobby(string? message)
    {
        StopRound();
        _phase = RoomPhase.Lobby;
        foreach (var member in _members)
        {
            member.Ready = false;
            member.Role = null;
            member.Character = null;
        }
        if (message is not null) Announce(message, MessageTone.Info);
        PublishState();
        _roomListChanged();
    }

    private void Announce(string text, MessageTone tone)
    {
        foreach (var member in _members) member.Connection.Client.GameEvent(text, tone).Forget();
    }

    private void PublishState()
    {
        var dto = new RoomDto(
            Code,
            _isPublic,
            _phase,
            _hostId,
            _settings.MinPlayers,
            GameRules.MaxPlayers,
            _members.Select(m => new RoomPlayerDto(
                m.Connection.Id,
                m.Connection.Name,
                m.Ready,
                m.Role,
                m.Character,
                m.Connection.Id == _hostId)).ToList());

        foreach (var member in _members) member.Connection.Client.RoomState(dto).Forget();
    }
}
