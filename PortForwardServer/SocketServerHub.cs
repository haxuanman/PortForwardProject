using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using PortForwardServer.Dto;
using System.Net;
using System.Net.Sockets;

namespace PortForwardServer
{
    public class SocketServerHub : Hub<ISocketServerHub>
    {

        private TcpListener _listener = new TcpListener(IPAddress.Any, 0);
        private string _connectionId = string.Empty;
        private static Dictionary<string, TcpClient> _listChildConnect = new();



        public override async Task OnConnectedAsync()
        {

            try
            {
                await base.OnConnectedAsync();

                var requestLocalPortQuery = new StringValues();

                Context?.GetHttpContext()?.Request.Query.TryGetValue("RequestServerLocalPort", out requestLocalPortQuery);

                var requestLocalPort = Convert.ToInt32(requestLocalPortQuery.FirstOrDefault(string.Empty));

                Console.WriteLine($"New client connect {Context?.ConnectionId}");

                _listener = new TcpListener(IPAddress.Any, requestLocalPort);

                _listener?.Start();

                Console.WriteLine($"Open local port {((IPEndPoint?)_listener?.LocalEndpoint)?.Port} for client {Context?.ConnectionId}");

                _connectionId = Context?.ConnectionId;

                _listener?.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), new HandleIncomingConnectionStateDto
                {
                    Clients = Clients
                });

            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString());

                Console.WriteLine(ex);
            }

        }



        private void HandleIncomingConnection(IAsyncResult result)
        {

            var states = result.AsyncState as HandleIncomingConnectionStateDto;

            var client = _listener?.EndAcceptTcpClient(result);

            _listener?.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), new HandleIncomingConnectionStateDto
            {
                Clients = Clients
            });

            HandleChildClientProxy(client!, states!.Clients!);

        }



        async void HandleChildClientProxy(TcpClient client, IHubCallerClients<ISocketServerHub> clients)
        {
            await HandleChildClient(client, clients);
        }



        async Task HandleChildClient(TcpClient client, IHubCallerClients<ISocketServerHub> clients)
        {
            var childClientName = string.Empty;
            try
            {

                childClientName = ((IPEndPoint?)client?.Client.RemoteEndPoint)?.ToString() ?? string.Empty;

                await clients.Caller.RequestChildClient(childClientName);

                _listChildConnect[childClientName] = client!;

                Console.WriteLine($"New child client of {_connectionId} connected: {childClientName}");

                var bufferSize = client?.ReceiveBufferSize ?? 2048;

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

                await clients.Caller.CloseChildClient(childClientName);

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
