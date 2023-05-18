namespace PortForwardServer
{
    public interface ISocketServerHub
    {

        Task RequestChildClient(string childClientName);

        Task CloseChildClient(string childClientName);

        Task ChildClientSocketRequest(string remoteClientName, string buffer);

    }
}
