using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public interface IHopCooldown
{
    double GetMs();
}

public class AnimalHop(Animal animal) : IHopCooldown
{
    private readonly Animal _animal = animal;

    public double GetMs() => Rules.HopCooldownMs(_animal);
}

public abstract class HopDecorator(IHopCooldown wrappee) : IHopCooldown
{
    protected readonly IHopCooldown _wrappee = wrappee;

    public virtual double GetMs() => _wrappee.GetMs();
}

public class RainDecorator(IHopCooldown wrappee) : HopDecorator(wrappee)
{
    public override double GetMs() => _wrappee.GetMs() * Rules.RainSlowdown;
}

public class AdrenalineDecorator(IHopCooldown wrappee) : HopDecorator(wrappee)
{
    public override double GetMs() => _wrappee.GetMs() * Rules.AdrenalineSpeedup;
}
