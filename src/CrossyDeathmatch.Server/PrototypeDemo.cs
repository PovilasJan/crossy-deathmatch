using System.Runtime.CompilerServices;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>Prints the memory addresses of a prototype and its shallow and deep copies (run with --prototype-demo true).</summary>
public static class PrototypeDemo
{
    public static void Run()
    {
        var prototype = new Obstacle { Kind = ObstacleKind.Car, Length = 1 };
        var registry = new PrototypeRegistry();
        registry.Add(ObstacleKind.Car, prototype);

        registry.UseDeepCopy = false;
        var shallow = registry.Create(ObstacleKind.Car);
        registry.UseDeepCopy = true;
        var deep = registry.Create(ObstacleKind.Car);

        Console.WriteLine("Object      Obstacle address      Hitbox address");
        Print("Prototype", prototype);
        Print("Shallow", shallow);
        Print("Deep", deep);

        Console.WriteLine();
        Console.WriteLine($"Shallow copy shares the prototype's Hitbox: {ReferenceEquals(shallow.Hitbox, prototype.Hitbox)}");
        Console.WriteLine($"Deep copy shares the prototype's Hitbox:    {ReferenceEquals(deep.Hitbox, prototype.Hitbox)}");

        shallow.Hitbox.Inset = 0.5;
        Console.WriteLine();
        Console.WriteLine("After shallow.Hitbox.Inset = 0.5:");
        Console.WriteLine($"  Prototype Inset: {prototype.Hitbox.Inset}");
        Console.WriteLine($"  Shallow Inset:   {shallow.Hitbox.Inset}");
        Console.WriteLine($"  Deep Inset:      {deep.Hitbox.Inset}");
    }

    private static void Print(string name, Obstacle obstacle)
    {
        var hitbox = obstacle.Hitbox;
        Console.WriteLine($"{name,-11} 0x{Address(ref obstacle):X16}    0x{Address(ref hitbox):X16}");
    }

    private static nint Address<T>(ref T obj) where T : class => Unsafe.As<T, nint>(ref obj);
}
