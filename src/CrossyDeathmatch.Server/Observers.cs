using Microsoft.AspNetCore.SignalR;

namespace CrossyDeathmatch.Server;

public interface IGameObserver
{
    void OnState(string json);
    void OnMessage(string text);
}

public class ClientObserver : IGameObserver
{
    private readonly IClientProxy _client;

    public ClientObserver(IClientProxy client)
    {
        _client = client;
    }

    public void OnState(string json)
    {
        _client.SendAsync("State", json);
    }

    public void OnMessage(string text)
    {
        _client.SendAsync("Message", text);
    }
}

public class ConsoleObserver : IGameObserver
{
    public void OnState(string json) { }

    public void OnMessage(string text)
    {
        ServerLog.GetInstance().Info(text);
    }
}
