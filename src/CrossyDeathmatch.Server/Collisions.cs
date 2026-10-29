using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// What a runner runs into on their tile: an obstacle that hits them, or a coin to pick up. It finds the
/// runner's lane through the <see cref="GameMap"/> facade; what a hit or a coin means is up to <see cref="Game"/>.
/// </summary>
public class Collisions(GameMap map)
{
    private readonly GameMap _map = map;

    /// <summary>
    /// The obstacle overlapping the runner, if any. The hitbox is a little narrower than the tile, so grazing
    /// a bumper is forgiven. Boulders don't count here: they only hurt when they land.
    /// </summary>
    public Obstacle? HitBy(Player runner) =>
        _map.GetLane(runner.Lane)?.Obstacles.FirstOrDefault(o =>
            o.Kind != ObstacleKind.Boulder && o.X + o.Hitbox.Inset < runner.X + 1 && o.X + o.Length - o.Hitbox.Inset > runner.X);

    /// <summary>Removes the coin under the runner. Returns false if there was none.</summary>
    public bool TakeCoin(Player runner) => _map.GetLane(runner.Lane)?.Coins.Remove(runner.X) == true;
}
