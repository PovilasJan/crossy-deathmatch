using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Obstacles;

public sealed class Train(int heading)
    : Obstacle(ObstacleKind.Train, heading > 0 ? -TrainLength : GameRules.GridWidth, TrainLength, heading * TrainSpeed)
{
    private const int TrainLength = 12;
    private const double TrainSpeed = 24;

    public override bool IsLethal(long now) => true;
}
