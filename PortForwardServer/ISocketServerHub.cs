namespace PortForwardServer
{
    public interface ISocketServerHub
    {

        Task ChildClientSocketReponse(string childClientName, string bufferString);

        Task RequestChildClient(string childClientName);

        Task CloseChildClient(string childClientName);

        Task ChildClientSocketRequest(string remoteClientName, string buffer);

    }
}
