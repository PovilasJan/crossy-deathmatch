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
        lane.Obstacles.Add(Game.NewCar(lane));
        return true;
    }
}

public class BoulderAbility : IAbilityStrategy
{
    public bool Use(GameState state, Lane? lane, int column, long now)
    {
        if (lane == null || column < 0 || column >= Rules.GridWidth || Game.IsBlocked(lane, column)) return false;
        lane.Obstacles.Add(new Obstacle
        {
            Kind = ObstacleKind.Boulder,
            X = column,
            LandsAt = now + Rules.BoulderFallMs,
            ExpiresAt = now + Rules.BoulderFallMs + Rules.BoulderLifeMs,
        });
        return true;
    }
}

public class FoxAbility : IAbilityStrategy
{
    public bool Use(GameState state, Lane? lane, int column, long now)
    {
        if (lane == null || lane.Type != LaneType.Grass || lane.Obstacles.Count(o => o.Kind == ObstacleKind.Fox) >= 2) return false;
        var fromLeft = Random.Shared.Next(2) == 0;
        lane.Obstacles.Add(new Obstacle
        {
            Kind = ObstacleKind.Fox,
            X = fromLeft ? 0 : Rules.GridWidth - 1,
            Speed = fromLeft ? 2.2 : -2.2,
            ExpiresAt = now + Rules.FoxLifeMs,
        });
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
