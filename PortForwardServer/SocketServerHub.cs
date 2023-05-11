using Microsoft.AspNetCore.SignalR;

namespace PortForwardServer
{
    public class SocketServerHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            Console.WriteLine($"New client connect {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"New client disconnect {Context.ConnectionId}");
            return base.OnDisconnectedAsync(exception);
        }
    }
}
