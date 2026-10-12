using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR;

namespace CrossyDeathmatch.Server;

/// <summary>The SignalR endpoint the clients call. Every call is passed straight to <see cref="Game"/>.</summary>
public class GameHub(Game game) : Hub
{
    public void Join(string name) => game.Join(Context.ConnectionId, name);

    public void PickAnimal(Animal animal) => game.PickAnimal(Context.ConnectionId, animal);

    public void SetReady(bool ready) => game.SetReady(Context.ConnectionId, ready);

    public void Move(int dx, int dy) => game.Move(Context.ConnectionId, dx, dy);

    public void UseAbility(Ability ability, int lane, int column) => game.UseAbility(Context.ConnectionId, ability, lane, column);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        game.Leave(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
