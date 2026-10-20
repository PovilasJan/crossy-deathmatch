using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public abstract class LaneBuilder
{
    protected Lane _lane = new();

    public void Reset(int index, LaneType type) => _lane = new Lane { Index = index, Type = type };

    public virtual void BuildTrees(Lane? previous, double difficulty) { }

    public virtual void BuildTraffic(double difficulty, long now) { }

    public virtual void BuildCoins() { }

    public Lane GetLane() => _lane;
}

public class GrassLaneBuilder : LaneBuilder
{
    public override void BuildTrees(Lane? previous, double difficulty)
    {
        if (_lane.Index < Game.StartLanes)
        {
            // Runners spawn here, so only the edges may have trees.
            if (Game.Chance(0.5)) _lane.Trees.Add(0);
            if (Game.Chance(0.5)) _lane.Trees.Add(Rules.GridWidth - 1);
            return;
        }

        // Keep 3 columns clear that were also clear on the lane below, so trees never wall the way.
        var open = Enumerable.Range(0, Rules.GridWidth)
            .Where(c => previous == null || !previous.Trees.Contains(c))
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();
        for (var column = 0; column < Rules.GridWidth; column++)
        {
            if (!open.Contains(column) && Game.Chance(0.12 + difficulty * 0.12)) _lane.Trees.Add(column);
        }
    }

    public override void BuildCoins()
    {
        if (_lane.Index >= Game.StartLanes) Game.AddCoin(_lane, 0.3);
    }
}

public class RoadLaneBuilder : LaneBuilder
{
    public override void BuildTraffic(double difficulty, long now)
    {
        var index = _lane.Index;
        _lane.Speed = (Game.Chance(0.5) ? 1 : -1) * (Game.Between(1.6, 2.8) + difficulty * 2.5);
        // Fill the road with cars so it doesn't start empty.
        for (var x = -2 + Game.Between(0, Game.MaxGap(index)); x < Rules.GridWidth + 1;)
        {
            var car = Game.NewCar(_lane, x);
            _lane.Obstacles.Add(car);
            x += car.Length + Game.Between(Game.MinGap(index), Game.MaxGap(index));
        }
    }

    public override void BuildCoins() => Game.AddCoin(_lane, 0.1);
}

public class RailLaneBuilder : LaneBuilder
{
    public override void BuildTraffic(double difficulty, long now)
    {
        _lane.Speed = (Game.Chance(0.5) ? 1 : -1) * 24;
        _lane.NextTrainAt = now + (long)Game.Between(3000, 8000);
    }
}
