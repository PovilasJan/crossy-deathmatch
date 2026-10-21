using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public abstract class LaneBuilder
{
    protected Lane _lane = new();

    public void Reset(int index, LaneType type) => _lane = new Lane { Index = index, Type = type };

    public virtual void BuildTrees() { }

    public virtual void BuildTraffic(long now) { }

    public virtual void BuildCoins() { }

    public Lane GetLane() => _lane;
}

public class GrassLaneBuilder : LaneBuilder
{
    public override void BuildTrees()
    {
        // The start lanes have no trees, so the runners have room to spawn.
        if (_lane.Index < Game.StartLanes) return;
        for (var column = 0; column < Rules.GridWidth; column++)
        {
            if (Game.Chance(0.15)) _lane.Trees.Add(column);
        }
    }

    public override void BuildCoins() => Game.AddCoin(_lane, 0.3);
}

public class RoadLaneBuilder : LaneBuilder
{
    public override void BuildTraffic(long now)
    {
        _lane.Speed = (Game.Chance(0.5) ? 1 : -1) * Game.Between(2, 4);
        // Fill the road with cars so it doesn't start empty.
        for (var x = -2.0; x < Rules.GridWidth + 1;)
        {
            var car = Game.NewCar(_lane, x);
            _lane.Obstacles.Add(car);
            x += car.Length + Game.Between(Game.MinCarGap, Game.MaxCarGap);
        }
    }

    public override void BuildCoins() => Game.AddCoin(_lane, 0.1);
}

public class RailLaneBuilder : LaneBuilder
{
    public override void BuildTraffic(long now)
    {
        _lane.Speed = (Game.Chance(0.5) ? 1 : -1) * 24;
        _lane.NextTrainAt = now + (long)Game.Between(3000, 8000);
    }
}
