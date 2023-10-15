namespace PortForwardServer
{
    public interface ISocketServerHub
    {

        Task CreateSessionAsync(Guid sessionId);

        Task DeleteSessionAsync(Guid sessionId);

        Task SendDataAsync(Guid sessionId, string data);

    }
}
