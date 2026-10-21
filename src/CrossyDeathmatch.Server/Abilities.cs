using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public interface IAbilityStrategy
{
    bool Use(GameState state, Lane? lane, int column, long now);
}

public class CarAbility : IAbilityStrategy
{
    public bool Use(GameState state, Lane? lane, int column, long now)
    {
        if (lane == null || lane.Type != LaneType.Road) return false;
        lane.Obstacles.Add(ObstacleFactory.CreateVehicle(lane, now));
        return true;
    }
}

public class BoulderAbility : IAbilityStrategy
{
    public bool Use(GameState state, Lane? lane, int column, long now)
    {
        if (lane == null || column < 0 || column >= Rules.GridWidth || Game.IsBlocked(lane, column)) return false;
        lane.Obstacles.Add(ObstacleFactory.Create(ObstacleKind.Boulder, lane, now, column));
        return true;
    }
}

public class FoxAbility : IAbilityStrategy
{
    public bool Use(GameState state, Lane? lane, int column, long now)
    {
        if (lane == null || lane.Type != LaneType.Grass || lane.Obstacles.Count(o => o.Kind == ObstacleKind.Fox) >= 2) return false;
        lane.Obstacles.Add(ObstacleFactory.Create(ObstacleKind.Fox, lane, now));
        return true;
    }
}

public class RainAbility : IAbilityStrategy
{
    public bool Use(GameState state, Lane? lane, int column, long now)
    {
        state.RainUntil = now + Rules.RainMs;
        return true;
    }
}
