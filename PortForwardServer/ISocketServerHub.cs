namespace PortForwardServer
{
    public interface ISocketServerHub
    {

        Task CreateSessionAsync(Guid sessionId);

        Task DeleteSessionAsync(Guid sessionId);

        Task SendDatasync(Guid sessionId, string data);

    }
}
