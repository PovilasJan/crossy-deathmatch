namespace CrossyDeathmatch.Server.Gameplay;

internal static class Rng
{
    public static double Between(double min, double max) => min + Random.Shared.NextDouble() * (max - min);

    public static int Int(int minInclusive, int maxInclusive) => Random.Shared.Next(minInclusive, maxInclusive + 1);

    public static bool Chance(double probability) => Random.Shared.NextDouble() < probability;

    public static T Pick<T>(IReadOnlyList<T> items) => items[Random.Shared.Next(items.Count)];
}
