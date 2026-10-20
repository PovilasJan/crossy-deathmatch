using System.Text.Json;
using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR;

namespace CrossyDeathmatch.Server;

/// <summary>
/// The whole game: the lobby, the round simulation and the endless map. Everything lives in one
/// <see cref="GameState"/>, and every method locks it, including the 20 Hz timer, so the hub
/// threads and the game loop never touch it at the same time.
/// </summary>
public class Game
{
    private const int LanesAhead = 20;
    private const int LanesBehind = 8;
    private const int StartLanes = 4;
    private static readonly int[] SpawnColumns = [6, 3, 9, 1];

    private readonly IHubContext<GameHub> _hub;
    private readonly GameState _state = new();
    private readonly Dictionary<string, IGameObserver> _observers = new();
    private readonly Timer _timer;
    private readonly int _minPlayers;
    private readonly int _roundSeconds;
    private long _lastTickAt = Environment.TickCount64;
    private long _endsAt;
    private LaneType _groupType;
    private int _groupLeft;

    private static readonly Dictionary<Ability, IAbilityStrategy> Abilities = new()
    {
        [Ability.Car] = new CarAbility(),
        [Ability.Boulder] = new BoulderAbility(),
        [Ability.Fox] = new FoxAbility(),
        [Ability.Rain] = new RainAbility(),
    };

    public Game(IHubContext<GameHub> hub, IConfiguration config)
    {
        _hub = hub;
        _minPlayers = Math.Clamp(config.GetValue("min-players", Rules.MaxPlayers), 1, Rules.MaxPlayers);
        _roundSeconds = Math.Max(10, config.GetValue("round-seconds", 180));
        Attach("console", new ConsoleObserver());
        _timer = new Timer(_ => Tick(), null, 0, 1000 / Rules.TickRate);
        Console.WriteLine($"  Min players to start: {_minPlayers}   Round length: {_roundSeconds}s");
    }

    private static long Now => Environment.TickCount64;

    private IEnumerable<Player> Runners => _state.Players.Where(p => !p.IsMastermind);

    // ---- called by GameHub ----

    public void Join(string id, string name)
    {
        name = (name ?? "").Trim();
        lock (_state)
        {
            var error =
                name.Length is 0 or > Rules.MaxNameLength ? $"Nickname must be 1 to {Rules.MaxNameLength} characters" :
                _state.Players.Any(p => p.Id == id) ? "You have already joined" :
                _state.Playing ? "A round is in progress, try again when it ends" :
                _state.Players.Count >= Rules.MaxPlayers ? "The game is full" :
                _state.Players.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ? "That nickname is taken" :
                null;
            if (error != null)
            {
                SendMessage(id, error);
                return;
            }

            var animal = Enum.GetValues<Animal>().FirstOrDefault(a => _state.Players.All(p => p.Animal != a));
            _state.Players.Add(new Player { Id = id, Name = name, Animal = animal });
            Attach(id, new ClientObserver(_hub.Clients.Client(id)));
            Announce($"{name} joined");
            SendState();
        }
    }

    public void Leave(string id)
    {
        lock (_state)
        {
            var player = Find(id);
            if (player == null) return;

            Detach(id);
            if (_state.Playing)
            {
                // Kept until the round ends so the scoreboard still shows them.
                player.Left = true;
                player.Out = true;
            }
            else
            {
                _state.Players.Remove(player);
            }
            Announce($"{player.Name} left");
            SendState();
            StartIfEveryoneReady();
        }
    }

    public void PickAnimal(string id, Animal animal)
    {
        lock (_state)
        {
            var player = Find(id);
            if (player == null || _state.Playing) return;
            player.Animal = animal;
            SendState();
        }
    }

    public void SetReady(string id, bool ready)
    {
        lock (_state)
        {
            var player = Find(id);
            if (player == null || _state.Playing) return;
            player.Ready = ready;
            SendState();
            StartIfEveryoneReady();
        }
    }

    public void Move(string id, int dx, int dy)
    {
        lock (_state)
        {
            var now = Now;
            var runner = Find(id);
            if (!_state.Playing || runner == null || runner.IsMastermind || runner.Out || runner.RespawnAt > 0) return;

            var cooldown = Rules.HopCooldownMs(runner.Animal) * (now < _state.RainUntil ? Rules.RainSlowdown : 1);
            if (now - runner.LastHopAt < cooldown) return;

            dx = Math.Sign(dx);
            dy = dx != 0 ? 0 : Math.Sign(dy);
            var x = runner.X + dx;
            var laneIndex = runner.Lane + dy;
            var lane = GetLane(laneIndex);
            if (x < 0 || x >= Rules.GridWidth) return;
            if (laneIndex < runner.MaxLane - Rules.BacktrackLimit) return;
            if (lane == null || IsBlocked(lane, x)) return;

            runner.X = x;
            runner.Lane = laneIndex;
            runner.LastHopAt = now;
            if (laneIndex > runner.MaxLane)
            {
                runner.Score += laneIndex - runner.MaxLane;
                runner.MaxLane = laneIndex;
            }
        }
    }

    public void UseAbility(string id, Ability ability, int laneIndex, int column)
    {
        lock (_state)
        {
            var now = Now;
            var mastermind = Find(id);
            if (!_state.Playing || mastermind == null || !mastermind.IsMastermind || mastermind.Left) return;

            var lane = GetLane(laneIndex);
            var name = Rules.Name(ability);
            string? error = null;
            if (now < mastermind.ReadyAt[(int)ability]) error = $"{name} is recharging";
            else if (mastermind.Energy < Rules.Cost(ability)) error = $"Not enough energy for {name}";
            else if (!Abilities[ability].Use(_state, lane, column, now)) error = $"{name} can't be used there";
            if (error != null)
            {
                SendMessage(id, error);
                return;
            }

            mastermind.Energy -= Rules.Cost(ability);
            mastermind.ReadyAt[(int)ability] = now + Rules.CooldownMs(ability);
            Announce($"Mastermind used {name}!");
        }
    }

    // ---- rounds ----

    private void StartIfEveryoneReady()
    {
        if (!_state.Playing && _state.Players.Count >= _minPlayers && _state.Players.All(p => p.Ready)) StartRound();
    }

    private void StartRound()
    {
        var now = Now;
        var players = _state.Players;
        var mastermind = players.Count >= 2 ? players[Random.Shared.Next(players.Count)] : null;

        var spawn = 0;
        foreach (var player in players)
        {
            player.Ready = false;
            player.IsMastermind = player == mastermind;
            player.X = player.IsMastermind ? 0 : SpawnColumns[spawn++ % SpawnColumns.Length];
            player.Lane = 1;
            player.MaxLane = 1;
            player.Lives = Rules.Lives(player.Animal);
            player.Score = 0;
            player.Coins = 0;
            player.Out = false;
            player.RespawnAt = 0;
            player.InvulnerableUntil = 0;
            player.LastHopAt = 0;
            player.Energy = Rules.StartEnergy;
            player.ReadyAt = new long[4];
        }

        _state.Lanes.Clear();
        _groupType = LaneType.Grass;
        _groupLeft = 0;
        while (_state.Lanes.Count <= LanesAhead) AddLane(now);

        _state.Playing = true;
        _state.Result = null;
        _state.RainUntil = 0;
        _endsAt = now + _roundSeconds * 1000L;
        Announce(mastermind != null ? $"{mastermind.Name} is the Mastermind!" : "The round has started!");
        SendState();
    }

    private void Tick()
    {
        lock (_state)
        {
            var now = Now;
            var dt = Math.Min(0.1, (now - _lastTickAt) / 1000.0);
            _lastTickAt = now;
            if (!_state.Playing) return;

            try
            {
                Simulate(now, dt);
            }
            catch (Exception ex)
            {
                // An exception on a timer thread would kill the server; log it and carry on instead.
                Console.WriteLine(ex);
            }
            SendState();
        }
    }

    private void Simulate(long now, double dt)
    {
        _state.TimeLeftMs = (int)Math.Max(0, _endsAt - now);
        foreach (var lane in _state.Lanes) UpdateLane(lane, dt, now);

        foreach (var runner in Runners.Where(r => !r.Out))
        {
            if (runner.RespawnAt > 0)
            {
                if (now >= runner.RespawnAt) Respawn(runner, now);
                continue;
            }

            var lane = GetLane(runner.Lane);
            if (lane == null) continue;

            // The hitbox is a little narrower than the tile, so grazing a bumper is forgiven.
            var hit = lane.Obstacles.FirstOrDefault(o =>
                o.Kind != ObstacleKind.Boulder && o.X < runner.X + 0.8 && o.X + o.Length > runner.X + 0.2);
            if (hit != null && now >= runner.InvulnerableUntil)
            {
                Hit(runner, Cause(hit.Kind), now);
                continue;
            }

            if (lane.Coins.Remove(runner.X))
            {
                runner.Coins++;
                runner.Score += Rules.PointsPerCoin;
            }
        }

        foreach (var mastermind in _state.Players.Where(p => p.IsMastermind))
        {
            mastermind.Energy = Math.Min(Rules.MaxEnergy, mastermind.Energy + Rules.EnergyPerSecond * dt);
        }

        // Generate lanes ahead of the leader and drop the ones far behind the last runner.
        var active = Runners.Where(r => !r.Out).ToList();
        if (active.Count > 0)
        {
            while (_state.Lanes[^1].Index < active.Max(r => r.Lane) + LanesAhead) AddLane(now);
            while (_state.Lanes[0].Index < active.Min(r => r.Lane) - LanesBehind) _state.Lanes.RemoveAt(0);
        }

        if (Runners.All(r => r.Out) || now >= _endsAt) EndRound();
    }

    private void EndRound()
    {
        var runners = Runners.ToList();
        var mastermind = _state.Players.FirstOrDefault(p => p.IsMastermind);
        var ranking = runners.OrderBy(r => r.Out).ThenByDescending(r => r.Score).ToList();

        var winner = runners.All(r => r.Out) && mastermind != null
            ? $"{mastermind.Name} (Mastermind) wins - every runner is out!"
            : $"{ranking[0].Name} wins with {ranking[0].Score} points!";
        var lines = ranking.Select((r, i) => $"{i + 1}. {r.Name} - {r.Score} points{(r.Out ? " (out)" : "")}");

        _state.Result = winner + "\n" + string.Join("\n", lines);
        _state.Playing = false;
        _state.Lanes.Clear();
        _state.Players.RemoveAll(p => p.Left);
        foreach (var player in _state.Players)
        {
            player.Ready = false;
            player.IsMastermind = false;
        }
        Announce(winner);
    }

    private void Hit(Player runner, string cause, long now)
    {
        runner.Lives--;
        if (runner.Lives <= 0)
        {
            runner.Out = true;
            Announce($"{runner.Name} {cause} and is OUT!");
            return;
        }
        runner.RespawnAt = now + Rules.RespawnDelayMs;
        Announce($"{runner.Name} {cause} ({runner.Lives} {(runner.Lives == 1 ? "life" : "lives")} left)");
    }

    /// <summary>Puts the runner on the nearest free grass tile at or behind where they died.</summary>
    private void Respawn(Player runner, long now)
    {
        foreach (var lane in _state.Lanes.Where(l => l.Index <= runner.Lane && l.Type == LaneType.Grass).OrderByDescending(l => l.Index))
        {
            var free = Enumerable.Range(0, Rules.GridWidth)
                .Where(c => !IsBlocked(lane, c))
                .OrderBy(c => Math.Abs(c - runner.X))
                .ToList();
            if (free.Count == 0) continue;

            runner.X = free[0];
            runner.Lane = lane.Index;
            break;
        }
        runner.RespawnAt = 0;
        runner.InvulnerableUntil = now + Rules.InvulnerableMs;
        runner.LastHopAt = now;
    }

    private static string Cause(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Car => "got flattened by a car",
        ObstacleKind.Truck => "got flattened by a truck",
        ObstacleKind.Train => "was hit by a train",
        _ => "was caught by a fox",
    };

    // ---- the map ----

    private void UpdateLane(Lane lane, double dt, long now)
    {
        foreach (var obstacle in lane.Obstacles)
        {
            obstacle.X += obstacle.Speed * dt;

            if (obstacle.Kind == ObstacleKind.Fox)
            {
                // A fox turns around at the edges instead of leaving.
                var max = Rules.GridWidth - 1;
                if (obstacle.X < 0)
                {
                    obstacle.X = -obstacle.X;
                    obstacle.Speed = Math.Abs(obstacle.Speed);
                }
                else if (obstacle.X > max)
                {
                    obstacle.X = 2 * max - obstacle.X;
                    obstacle.Speed = -Math.Abs(obstacle.Speed);
                }
            }

            if (obstacle.Kind == ObstacleKind.Boulder && !obstacle.Landed && now >= obstacle.LandsAt)
            {
                obstacle.Landed = true;
                var crushed = Runners.Where(r => r.Lane == lane.Index && r.X == (int)obstacle.X && !r.Out &&
                                                 r.RespawnAt == 0 && now >= r.InvulnerableUntil).ToList();
                foreach (var runner in crushed) Hit(runner, "was crushed by a boulder", now);
            }
        }

        lane.Obstacles.RemoveAll(o =>
            (o.ExpiresAt > 0 && now >= o.ExpiresAt) ||
            (o.Kind != ObstacleKind.Fox && o.Speed > 0 && o.X > Rules.GridWidth + 1) ||
            (o.Kind != ObstacleKind.Fox && o.Speed < 0 && o.X + o.Length < -1));

        if (lane.Type == LaneType.Road)
        {
            lane.SpawnTimer -= dt;
            if (lane.SpawnTimer > 0) return;

            var car = NewCar(lane);
            var gap = MinGap(lane.Index);
            var entryBusy = lane.Obstacles.Any(o => o.Kind is ObstacleKind.Car or ObstacleKind.Truck &&
                                                    o.X < car.X + car.Length + gap && o.X + o.Length > car.X - gap);
            if (entryBusy)
            {
                lane.SpawnTimer = 0.15;
                return;
            }
            lane.Obstacles.Add(car);
            lane.SpawnTimer = (car.Length + Between(MinGap(lane.Index), MaxGap(lane.Index))) / Math.Abs(lane.Speed);
        }
        else if (lane.Type == LaneType.Rail && now >= lane.NextTrainAt)
        {
            lane.Obstacles.Add(new Obstacle
            {
                Kind = ObstacleKind.Train,
                Length = 12,
                Speed = lane.Speed,
                X = lane.Speed > 0 ? -12 : Rules.GridWidth,
            });
            lane.NextTrainAt = now + (long)Between(5000, 10000);
        }
    }

    /// <summary>Adds the next lane on top of the map. Roads and rails come in groups, with grass in between.</summary>
    private void AddLane(long now)
    {
        var previous = _state.Lanes.Count > 0 ? _state.Lanes[^1] : null;
        var index = previous == null ? 0 : previous.Index + 1;
        var difficulty = Math.Min(1.0, index / 150.0);
        var lane = new Lane { Index = index };

        if (index >= StartLanes)
        {
            if (_groupLeft <= 0) PickNextGroup(difficulty);
            _groupLeft--;
            lane.Type = _groupType;
        }

        var heading = Chance(0.5) ? 1 : -1;
        switch (lane.Type)
        {
            case LaneType.Grass when index < StartLanes:
                // Runners spawn here, so only the edges may have trees.
                if (Chance(0.5)) lane.Trees.Add(0);
                if (Chance(0.5)) lane.Trees.Add(Rules.GridWidth - 1);
                break;

            case LaneType.Grass:
                // Keep 3 columns clear that were also clear on the lane below, so trees never wall the way.
                var open = Enumerable.Range(0, Rules.GridWidth)
                    .Where(c => previous == null || !previous.Trees.Contains(c))
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(3)
                    .ToList();
                for (var column = 0; column < Rules.GridWidth; column++)
                {
                    if (!open.Contains(column) && Chance(0.12 + difficulty * 0.12)) lane.Trees.Add(column);
                }
                AddCoin(lane, 0.3);
                break;

            case LaneType.Road:
                lane.Speed = heading * (Between(1.6, 2.8) + difficulty * 2.5);
                // Fill the road with cars so it doesn't start empty.
                for (var x = -2 + Between(0, MaxGap(index)); x < Rules.GridWidth + 1;)
                {
                    var car = NewCar(lane, x);
                    lane.Obstacles.Add(car);
                    x += car.Length + Between(MinGap(index), MaxGap(index));
                }
                AddCoin(lane, 0.1);
                break;

            case LaneType.Rail:
                lane.Speed = heading * 24;
                lane.NextTrainAt = now + (long)Between(3000, 8000);
                break;
        }

        _state.Lanes.Add(lane);
    }

    private void PickNextGroup(double difficulty)
    {
        if (_groupType != LaneType.Grass)
        {
            _groupType = LaneType.Grass;
            _groupLeft = Chance(0.35 - difficulty * 0.2) ? 2 : 1;
        }
        else if (Chance(0.7))
        {
            _groupType = LaneType.Road;
            _groupLeft = Random.Shared.Next(1, 3 + (int)Math.Round(difficulty * 3));
        }
        else
        {
            _groupType = LaneType.Rail;
            _groupLeft = difficulty > 0.4 ? Random.Shared.Next(1, 3) : 1;
        }
    }

    /// <summary>A car or truck for a road. Without an X it enters from the lane's upstream edge.</summary>
    internal static Obstacle NewCar(Lane lane, double? x = null)
    {
        var truck = Chance(0.25);
        var length = truck ? 2 : 1;
        return new Obstacle
        {
            Kind = truck ? ObstacleKind.Truck : ObstacleKind.Car,
            Length = length,
            Speed = lane.Speed,
            X = x ?? (lane.Speed > 0 ? -length : Rules.GridWidth),
        };
    }

    private static void AddCoin(Lane lane, double chance)
    {
        var free = Enumerable.Range(0, Rules.GridWidth).Where(c => !lane.Trees.Contains(c)).ToList();
        if (free.Count > 0 && Chance(chance)) lane.Coins.Add(free[Random.Shared.Next(free.Count)]);
    }

    private Lane? GetLane(int index)
    {
        if (_state.Lanes.Count == 0) return null;
        var position = index - _state.Lanes[0].Index;
        return position >= 0 && position < _state.Lanes.Count ? _state.Lanes[position] : null;
    }

    internal static bool IsBlocked(Lane lane, int column) =>
        lane.Trees.Contains(column) ||
        lane.Obstacles.Any(o => o.Kind == ObstacleKind.Boulder && o.Landed && (int)o.X == column);

    private static double MinGap(int index) => Math.Max(1.5, 2.5 - Math.Min(1.0, index / 150.0));

    private static double MaxGap(int index) => 6 - Math.Min(1.0, index / 150.0) * 2;

    private static double Between(double min, double max) => min + Random.Shared.NextDouble() * (max - min);

    private static bool Chance(double probability) => Random.Shared.NextDouble() < probability;

    private Player? Find(string id) => _state.Players.FirstOrDefault(p => p.Id == id);

    // ---- sending (Observer: Game is the subject) ----

    private void Attach(string id, IGameObserver observer)
    {
        _observers[id] = observer;
    }

    private void Detach(string id)
    {
        _observers.Remove(id);
    }

    /// <summary>Serialised while the lock is held, so the game loop can't change the state halfway through.</summary>
    private void SendState()
    {
        _state.Time = Now;
        var json = JsonSerializer.Serialize(_state);
        foreach (var observer in _observers.Values)
        {
            observer.OnState(json);
        }
    }

    private void Announce(string text)
    {
        foreach (var observer in _observers.Values)
        {
            observer.OnMessage(text);
        }
    }

    /// <summary>Only for errors, which can come before the player has joined and has an observer.</summary>
    private void SendMessage(string id, string text) => _ = _hub.Clients.Client(id).SendAsync("Message", text);
}
