using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public class PrototypeRegistry
{
    private readonly Dictionary<ObstacleKind, IPrototype<Obstacle>> _items = new();

    public void Add(ObstacleKind kind, IPrototype<Obstacle> prototype) => _items[kind] = prototype;

    public Obstacle Create(ObstacleKind kind) => _items[kind].Clone();
}
