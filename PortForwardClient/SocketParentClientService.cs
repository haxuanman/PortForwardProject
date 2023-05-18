using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PortForwardClient.Common;
using System.Net;
using System.Net.Sockets;

namespace PortForwardClient
{
    public class SocketParentClientService : IHostedService
    {

        private readonly HubConnection _connection;
        private readonly static Dictionary<string, TcpClient> _listChildConnect = new();
        private readonly IConfiguration _configuration;


        public SocketParentClientService(IConfiguration configuration)
        {

            _configuration = configuration;

            _connection = new HubConnectionBuilder()
                .ConfigureLogging(logging =>
                {
                    // Log to the Console
                    logging.AddConsole();

                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .WithAutomaticReconnect(new SignalrAlwaysRetryPolicy())
                .WithUrl($"{configuration["ServerUrl"]}/ServerSocketHub?requestServerLocalPort={_configuration.GetValue<int>("RequestServerLocalPort")}")
                .Build();

            _connection.On("RequestChildClient", new Type[] { typeof(string) }, RequestChildClient, new object());

            _connection.On("ChildClientSocketRequest", new Type[] { typeof(string), typeof(string) }, ChildClientSocketRequest, new object());

            _connection.On("CloseChildClient", new Type[] { typeof(string) }, CloseChildClient, new object());

        }



        Task CloseChildClient(object?[] args0, object arg1)
        {

            var remoteChildClientName = args0[0]?.ToString() ?? string.Empty;

            try
            {

                var childClient = _listChildConnect[remoteChildClientName];

                Console.WriteLine($"Closed child client port {((IPEndPoint?)childClient?.Client?.LocalEndPoint)?.Port} for client {remoteChildClientName}");

                childClient?.Close();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return Task.CompletedTask;

        }



        async Task RequestChildClient(object?[] args, object input)
        {

            var remoteChildClientName = args[0]?.ToString() ?? string.Empty;

            var client = new TcpClient();

            await client.ConnectAsync("localhost", _configuration.GetValue<int>("ClientSharedLocalPort"));

            Console.WriteLine($"Create child client port {((IPEndPoint?)client?.Client.LocalEndPoint)?.Port} for client {remoteChildClientName}");

            _listChildConnect[remoteChildClientName] = client!;

            HandleChildSocketProxy(remoteChildClientName, client);

        }



        async void HandleChildSocketProxy(string remoteChildClientName, TcpClient? client)
        {
            await HandleChildSocket(remoteChildClientName, client);
        }



        async Task HandleChildSocket(string remoteChildClientName, TcpClient? client)
        {
            try
            {

                var bufferSize = client!.ReceiveBufferSize;

                while (client?.Connected ?? false)
                {

                    var buffer = new byte[bufferSize];

                    var stream = client!.GetStream();

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    Console.WriteLine($"{remoteChildClientName} {byteRead}");

                    var bufferString = Convert.ToBase64String(buffer.Take(byteRead).ToArray());

                    Console.WriteLine($"Reponse {remoteChildClientName}: {bufferString}");

                    await _connection.InvokeCoreAsync("ChildClientSocketReponse", new[] { remoteChildClientName, bufferString });

                }
            }
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString());

                Console.WriteLine(ex.Message);
            }
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
            catch (Exception ex)
            {
                await File.AppendAllTextAsync("logs.txt", ex.ToString());

                Console.WriteLine(ex);
            }

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
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
