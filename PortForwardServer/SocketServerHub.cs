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

                Console.WriteLine($"Open local port {((IPEndPoint?)_listener?.LocalEndpoint)?.Port} for client {Context.ConnectionId}");

                _connectionId = Context.ConnectionId;

                HandleParentClientProxy(Context, Clients);

            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString()); 
                
                Console.WriteLine(ex);
            }

        }



        async void HandleParentClientProxy(HubCallerContext context, IHubCallerClients<ISocketServerHub> clients)
        {
            await HandleParentClient(context, clients);
        }



        async Task HandleParentClient(HubCallerContext context, IHubCallerClients<ISocketServerHub> clients)
        {

            try
            {

                while (_listener != null)
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    var childClientName = ((IPEndPoint?)client?.Client.LocalEndPoint)?.ToString() ?? string.Empty;

                    await clients.Caller.RequestChildClient(childClientName);

                    _listChildConnect[childClientName] = client!;

                    Console.WriteLine($"New child client of {_connectionId} connected: {childClientName}");

                    await HandleChildClient(childClientName, client!, clients);

                }
            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString());

                Console.WriteLine(ex);
            }

        }



        async Task HandleChildClient(string childClientName, TcpClient client, IHubCallerClients<ISocketServerHub> clients)
        {
            try
            {
                var bufferSize = client.ReceiveBufferSize;

                while (client?.Connected ?? false)
                {

                    var buffer = new byte[bufferSize];

                    var stream = client.GetStream();

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    Console.WriteLine($"{childClientName} {byteRead}");

                    buffer = buffer.Take(byteRead).ToArray();

                    var bufferString = Convert.ToBase64String(buffer);

                    Console.WriteLine($"Request {childClientName}: {bufferString}");

                    await clients.Caller.ChildClientSocketRequest(childClientName, bufferString);

                }

                await File.AppendAllTextAsync("logs.txt", $"Child client of {_connectionId} disconnected: {childClientName} out");

            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString());

                Console.WriteLine($"Child client {childClientName}: {ex.Message}");
            }
            finally
            {
                await File.AppendAllTextAsync("logs.txt", $"Child client of {_connectionId} disconnected: {childClientName}");

                _listChildConnect.Remove(childClientName);
                Console.WriteLine($"Child client of {_connectionId} disconnected: {childClientName}");
            }
        }



        public override async Task OnDisconnectedAsync(Exception? exception)
        {

            await base.OnDisconnectedAsync(exception);

            _listener?.Stop();

            Console.WriteLine($"Client disconnected {_connectionId}");

            Console.WriteLine($"Closed local port {((IPEndPoint?)_listener?.LocalEndpoint)?.Port} for client {Context.ConnectionId}");

        }



        [HubMethodName("ChildClientSocketReponse")]
        public async Task ChildClientSocketReponse(string childClientName, string bufferString)
        {

            try
            {

                await File.AppendAllTextAsync("logs.txt", "ChildClientSocketReponse");

                var childClient = _listChildConnect[childClientName];

                if (!(childClient?.Connected ?? false)) return;

                await childClient.GetStream().WriteAsync(Convert.FromBase64String(bufferString));

            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString());
                Console.WriteLine(ex);
            }

        }

    }
}
