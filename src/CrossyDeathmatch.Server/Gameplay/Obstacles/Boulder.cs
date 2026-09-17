using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Obstacles;

/// <summary>
/// Dropped by the Mastermind. While falling it is only a warning shadow; on the tick it lands it
/// crushes whoever stands on the tile, and afterwards it blocks the tile until it expires.
/// </summary>
public sealed class Boulder(int column, long landsAt, long expiresAt) : Obstacle(ObstacleKind.Boulder, column, 1, 0)
{
    private bool _landed;
    private bool _landedThisTick;

    public override void Update(double dt, long now)
    {
        _landedThisTick = false;
        if (_landed || now < landsAt) return;
        _landed = true;
        _landedThisTick = true;
    }

    public override bool IsLethal(long now) => _landedThisTick;

    public override bool BlocksColumn(int column) => _landed && column == (int)X;

    public override bool IsExpired(long now) => now >= expiresAt;

    public override ObstacleDto ToDto(int lane) => base.ToDto(lane) with { Landed = _landed };
}
