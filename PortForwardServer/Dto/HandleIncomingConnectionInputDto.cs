using Microsoft.AspNetCore.SignalR;

namespace PortForwardServer.Dto
{
    public class HandleIncomingConnectionStateDto
    {
        public IHubCallerClients<ISocketServerHub>? Clients { get; set; }
    }
}
