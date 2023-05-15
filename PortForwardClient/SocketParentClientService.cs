using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortForwardClient.Common;
using PortForwardServer;
using System.Net;
using System.Net.Sockets;

namespace PortForwardClient
{
    public class SocketParentClientService : IHostedService
    {

        private readonly HubConnection _connection;
        private static Dictionary<string, TcpClient> _listChildConnect = new();


        public SocketParentClientService(IConfiguration configuration)
        {

            _connection = new HubConnectionBuilder()
                .ConfigureLogging(logging =>
                {
                    // Log to the Console
                    logging.AddConsole();

                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .WithAutomaticReconnect(new SignalrAlwaysRetryPolicy())
                .WithUrl($"{configuration["ServerUrl"]}/ServerSocketHub")
                .Build();

            _connection.On("RequestChildClient", new Type[] { typeof(string) }, RequestChildClient, new object());

            _connection.On("ChildClientSocketRequest", new Type[] { typeof(string), typeof(string) }, ChildClientSocketRequest, new object());

        }



        async Task RequestChildClient(object?[] args, object input)
        {

            var remoteClientName = args[0]?.ToString() ?? string.Empty;

            var client = new TcpClient();

            await client.ConnectAsync("localhost", 3389);

            Console.WriteLine($"Create child client port {((IPEndPoint?)client?.Client.LocalEndPoint)?.Port} for client {remoteClientName}");

            _listChildConnect[remoteClientName] = client!;

            HandleChildSocketProxy(remoteClientName, client);

        }



        async void HandleChildSocketProxy(string remoteClientName, TcpClient? client)
        {
            await HandleChildSocket(remoteClientName, client);
        }



        async Task HandleChildSocket(string remoteClientName, TcpClient? client)
        {
            try
            {
                while (client?.Connected ?? false)
                {

                    var stream = client.GetStream();

                    var buffer = new byte[2048];

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    var bufferString = Convert.ToBase64String(buffer.Take(byteRead).ToArray());

                    await _connection.InvokeCoreAsync("ChildClientSocketReponse", new[] { remoteClientName, bufferString });

                }
            } catch (Exception ex) { Console.WriteLine(ex); }
            finally { Console.WriteLine($"Closed child client port {((IPEndPoint?)client?.Client.LocalEndPoint)?.Port} for client {remoteClientName}"); }
        }



        async Task ChildClientSocketRequest(object?[] args, object input)
        {
            try
            {

                var remoteClientName = args[0]?.ToString() ?? string.Empty;

                var buffer = Convert.FromBase64String(args[1]?.ToString() ?? string.Empty);

                var childClient = _listChildConnect[remoteClientName];

                await childClient.GetStream().WriteAsync(buffer);

            }
            catch (Exception ex) { Console.WriteLine(ex); }

        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _connection.StartAsync();
        }



        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _connection.StopAsync();
            } catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
