using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Rendering;

/// <summary>Turns the server's instant tile-to-tile moves into a short eased hop with a little jump arc.</summary>
internal sealed class RunnerAnimator
{
    private const float HopDurationMs = 110f;

    private sealed class Track
    {
        public float FromX, FromLane, ToX, ToLane;
        public long StartedAt;
        public bool Hopping;
    }

    private readonly Dictionary<string, Track> _tracks = [];

    public void Clear() => _tracks.Clear();

    public (float X, float Lane, float Jump) Resolve(RunnerDto runner, long now)
    {
        if (!_tracks.TryGetValue(runner.Id, out var track))
        {
            _tracks[runner.Id] = new Track { FromX = runner.X, FromLane = runner.Lane, ToX = runner.X, ToLane = runner.Lane };
            return (runner.X, runner.Lane, 0);
        }

        var moved = Math.Abs(runner.Lane - track.ToLane) >= 0.5f || Math.Abs(runner.X - track.ToX) >= 0.5f;
        if (moved)
        {
            var (x, lane, _) = Current(track, now);
            var teleported = runner.State != RunnerState.Alive || Math.Abs(runner.Lane - lane) > 2.5f || Math.Abs(runner.X - x) > 2.5f;
            track.FromX = teleported ? runner.X : x;
            track.FromLane = teleported ? runner.Lane : lane;
            track.StartedAt = now;
            track.Hopping = !teleported;
        }
        else if (!track.Hopping)
        {
            track.FromX = runner.X;
            track.FromLane = runner.Lane;
        }

        track.ToX = runner.X;
        track.ToLane = runner.Lane;
        return Current(track, now);
    }

    private static (float X, float Lane, float Jump) Current(Track track, long now)
    {
        if (!track.Hopping) return (track.ToX, track.ToLane, 0);

        var progress = (now - track.StartedAt) / HopDurationMs;
        if (progress >= 1)
        {
            track.Hopping = false;
            return (track.ToX, track.ToLane, 0);
        }

        var eased = 1 - (1 - progress) * (1 - progress);
        return (
            track.FromX + (track.ToX - track.FromX) * eased,
            track.FromLane + (track.ToLane - track.FromLane) * eased,
            MathF.Sin(progress * MathF.PI));
    }
}
