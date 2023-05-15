using Microsoft.AspNetCore.SignalR;
using System.Net.Sockets;

namespace PortForwardServer.Dto
{
    public class HandleChildClientProxyDto
    {
        public string childClientName { get; set; }
        public TcpClient client { get; set; }
        public IHubCallerClients<ISocketServerHub> clients { get; set; }
    }
}
