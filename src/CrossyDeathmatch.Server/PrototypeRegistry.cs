using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public class PrototypeRegistry
{
    private readonly Dictionary<ObstacleKind, IPrototype<Obstacle>> _items = new();

    public bool UseDeepCopy { get; set; } = true;

    public void Add(ObstacleKind kind, IPrototype<Obstacle> prototype) => _items[kind] = prototype;

    public Obstacle Create(ObstacleKind kind) =>
        UseDeepCopy ? _items[kind].DeepClone() : _items[kind].ShallowClone();
}
