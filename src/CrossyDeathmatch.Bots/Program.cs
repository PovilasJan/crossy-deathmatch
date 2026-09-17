using CrossyDeathmatch.Bots;

// Fills a room with computer players so the game can be tried without four people.
//
//   dotnet run --project src/CrossyDeathmatch.Bots -- --count 3 --room ABCD
//   dotnet run --project src/CrossyDeathmatch.Bots -- --count 4 --exit-after-game
//
// Without --room the first bot creates a public room and prints its code.

var options = BotOptions.Parse(args);
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var bots = new List<Bot>();
var roomCode = options.Room;
try
{
    for (var i = 0; i < options.Count; i++)
    {
        var bot = new Bot($"Bot{i + 1}-{Random.Shared.Next(100, 1000)}", options);
        await bot.ConnectAsync(cancellation.Token);
        bots.Add(bot);

        if (roomCode is null)
        {
            roomCode = await bot.CreateRoomAsync(cancellation.Token);
            Console.WriteLine($"{bot.Name} created room {roomCode}");
        }
        else
        {
            await bot.JoinRoomAsync(roomCode, cancellation.Token);
            Console.WriteLine($"{bot.Name} joined room {roomCode}");
        }
    }

    if (options.ExitAfterGame)
    {
        var result = await bots[0].FirstGameOver.WaitAsync(TimeSpan.FromMinutes(10), cancellation.Token);
        await Task.Delay(500);
        Console.WriteLine();
        Console.WriteLine($"Round over: {result.WinnerName} ({result.WinnerRole}) — {result.Reason}");
        foreach (var entry in result.Ranking)
        {
            Console.WriteLine($"  {entry.Name,-14} {entry.Character,-8} score {entry.Score,4}  coins {entry.Coins,3}  lane {entry.MaxLane,4}  {(entry.Survived ? "survived" : "out")}");
        }
        foreach (var bot in bots) Console.WriteLine($"  {bot.Name,-14} received {bot.SnapshotCount} snapshots, {bot.ErrorCount} error messages");
    }
    else
    {
        Console.WriteLine("Bots are playing. Press Ctrl+C to stop.");
        await Task.Delay(Timeout.Infinite, cancellation.Token);
    }
}
catch (OperationCanceledException)
{
    // Ctrl+C
}
finally
{
    foreach (var bot in bots) await bot.DisposeAsync();
}
