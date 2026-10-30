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

        Console.WriteLine("Objektas      Objekto adresas      Hitbox adresas");
        Print("Prototype", prototype);
        Print("Shallow", shallow);
        Print("Deep", deep);
    }

    private static void Print(string name, Obstacle obstacle)
    {
        var hitbox = obstacle.Hitbox;
        Console.WriteLine($"{name,-11}   {Address(ref obstacle):X16}     {Address(ref hitbox):X16}");
    }

    private static nint Address<T>(ref T obj) where T : class => Unsafe.As<T, nint>(ref obj);
}
