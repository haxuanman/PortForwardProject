using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.Sockets;

namespace PortForwardServer
{
    public class SocketServerHub : Hub<ISocketServerHub>
    {

        private readonly TcpListener? _listener;
        private string _connectionId;
        private static Dictionary<string, TcpClient> _listChildConnect = new();


        public SocketServerHub()
        {
            _listener = new TcpListener(IPAddress.Any, 0);
        }



        public override async Task OnConnectedAsync()
        {

            try
            {
                await base.OnConnectedAsync();

                Console.WriteLine($"New client connect {Context.ConnectionId}");

                _listener?.Start();

                Console.WriteLine($"Open local port {((IPEndPoint)_listener?.LocalEndpoint).Port} for client {Context.ConnectionId}");

                _connectionId = Context.ConnectionId;

                HandleParentClientProxy(Context, Clients);

            }
            catch (Exception ex) { Console.WriteLine(ex); }

        }



        async void HandleParentClientProxy(HubCallerContext context, IHubCallerClients<ISocketServerHub> clients)
        {
            await HandleParentClient(Context, Clients);
        }



        async Task HandleParentClient(HubCallerContext context, IHubCallerClients<ISocketServerHub> clients)
        {

            try
            {

                while (_listener != null)
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    var childClientName = ((IPEndPoint)client.Client.LocalEndPoint).ToString() ?? string.Empty;

                    await clients.Client(context.ConnectionId).RequestChildClient(childClientName);

                    _listChildConnect[childClientName] = client;

                    await HandleClient(childClientName, client, clients);

                    Console.WriteLine("New Child Client Connect");

                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }

        }



        async Task HandleClient(string childClientName, TcpClient client, IHubCallerClients<ISocketServerHub> clients)
        {
            try
            {
                while (client?.Connected ?? false)
                {

                    var stream = client.GetStream();

                    var buffer = new byte[2048];

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    buffer = buffer.Take(byteRead).ToArray();

                    await clients.Client(_connectionId).ChildClientSocketRequest(childClientName, Convert.ToBase64String(buffer));

                }
            } catch (Exception ex) { Console.WriteLine(ex); }
        }



        public override Task OnDisconnectedAsync(Exception? exception)
        {

            _listener?.Stop();

            Console.WriteLine($"New client disconnect {_connectionId}");
            return base.OnDisconnectedAsync(exception);
        }



        [HubMethodName("ChildClientSocketReponse")]
        public async Task ChildClientSocketReponse(string childClientName, string bufferString)
        {

            try
            {

                var childClient = _listChildConnect[childClientName];

                if (!(childClient?.Connected ?? false)) return;

                await childClient.GetStream().WriteAsync(Convert.FromBase64String(bufferString));

            }
            catch (Exception ex) { Console.WriteLine(ex); }

        }

    }
}
