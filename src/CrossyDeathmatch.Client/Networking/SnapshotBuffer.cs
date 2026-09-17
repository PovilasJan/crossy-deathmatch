using System.Diagnostics;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Networking;

/// <summary>
/// Keeps the last few server snapshots and renders slightly in the past, blending between the two
/// snapshots around that moment. Moving things glide at 60 fps even though the server sends 20.
/// </summary>
public sealed class SnapshotBuffer
{
    private const int InterpolationDelayMs = 90;
    private const int MaxSnapshots = 30;

    private readonly object _sync = new();
    private readonly List<SnapshotDto> _snapshots = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double? _serverClockOffset;

    public SnapshotDto? Latest
    {
        get { lock (_sync) return _snapshots.Count > 0 ? _snapshots[^1] : null; }
    }

    public void Push(SnapshotDto snapshot)
    {
        lock (_sync)
        {
            _snapshots.Add(snapshot);
            if (_snapshots.Count > MaxSnapshots) _snapshots.RemoveAt(0);

            double offset = snapshot.ServerTimeMs - _clock.ElapsedMilliseconds;
            _serverClockOffset = _serverClockOffset is { } previous ? previous + (offset - previous) * 0.1 : offset;
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _snapshots.Clear();
            _serverClockOffset = null;
        }
    }

    public SnapshotDto? Sample()
    {
        lock (_sync)
        {
            if (_snapshots.Count == 0 || _serverClockOffset is not { } offset) return null;

            var renderTime = _clock.ElapsedMilliseconds + offset - InterpolationDelayMs;
            var newer = _snapshots.FindIndex(s => s.ServerTimeMs >= renderTime);
            if (newer < 0) return _snapshots[^1];
            if (newer == 0) return _snapshots[0];

            var a = _snapshots[newer - 1];
            var b = _snapshots[newer];
            var t = (float)((renderTime - a.ServerTimeMs) / Math.Max(1, b.ServerTimeMs - a.ServerTimeMs));
            return Interpolate(a, b, Math.Clamp(t, 0f, 1f));
        }
    }

    private static SnapshotDto Interpolate(SnapshotDto a, SnapshotDto b, float t)
    {
        var previousObstacles = a.Obstacles.ToDictionary(o => o.Id);
        var obstacles = b.Obstacles
            .Select(o => previousObstacles.TryGetValue(o.Id, out var p) && Math.Abs(p.X - o.X) < 3
                ? o with { X = Lerp(p.X, o.X, t) }
                : o)
            .ToList();

        // Hops stay discrete here (the renderer animates them); only log drift is blended.
        var previousRunners = a.Runners.ToDictionary(r => r.Id);
        var runners = b.Runners
            .Select(r => previousRunners.TryGetValue(r.Id, out var p) && p.Lane == r.Lane && Math.Abs(p.X - r.X) < 0.5f
                ? r with { X = Lerp(p.X, r.X, t) }
                : r)
            .ToList();

        return b with { Obstacles = obstacles, Runners = runners };
    }

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;
}
