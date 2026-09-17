using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>The endless map: lanes are generated ahead of the leading runner and dropped far behind the last one.</summary>
public sealed class GameMap
{
    private readonly Dictionary<int, Lane> _lanes = [];
    private readonly MapGenerator _generator = new();

    public int LowestIndex { get; private set; }
    public int HighestIndex { get; private set; } = -1;

    public Lane? GetLane(int index) => _lanes.GetValueOrDefault(index);

    public void GenerateUntil(int index)
    {
        while (HighestIndex < index)
        {
            HighestIndex++;
            _lanes[HighestIndex] = _generator.CreateLane(HighestIndex);
        }
    }

    public void PruneBelow(int index)
    {
        while (LowestIndex < index && LowestIndex < HighestIndex)
        {
            _lanes.Remove(LowestIndex);
            LowestIndex++;
        }
    }

    public void Update(double dt, long now)
    {
        foreach (var lane in _lanes.Values) lane.Update(dt, now);
    }

    public IEnumerable<Lane> LanesBetween(int from, int to)
    {
        for (var index = Math.Max(from, LowestIndex); index <= Math.Min(to, HighestIndex); index++)
        {
            yield return _lanes[index];
        }
    }

    /// <summary>Nearest free grass tile at or behind the given lane (ahead only if nothing is behind).</summary>
    public (int Column, int Lane) FindRespawnSpot(int laneIndex, int column)
    {
        for (var index = laneIndex; index >= LowestIndex; index--)
        {
            if (FreeGrassColumn(index, column) is { } free) return (free, index);
        }
        for (var index = laneIndex + 1; index <= HighestIndex; index++)
        {
            if (FreeGrassColumn(index, column) is { } free) return (free, index);
        }
        return (column, laneIndex);
    }

    private int? FreeGrassColumn(int index, int column) =>
        GetLane(index) is { Type: LaneType.Grass } lane ? lane.NearestFreeColumn(column) : null;
}
