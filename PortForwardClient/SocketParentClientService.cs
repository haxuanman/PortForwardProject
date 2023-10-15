using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using PortForwardClient.Common;
using PortForwardClient.Services;
using System.Net;
using System.Net.Sockets;

namespace PortForwardClient
{
    public class SocketParentClientService : IHostedService
    {

        private readonly HubConnection _connection;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        private static readonly Dictionary<Guid, TcpClient> _listSessionConnect = new();


        public SocketParentClientService(
            IConfiguration configuration,
            ILogger<SocketParentClientService> logger
            )
        {

            _logger = logger;

            _configuration = configuration;

            var url = new UriBuilder($"{configuration["ServerUrl"]}/ServerSocketHub?requestServerLocalPort={_configuration.GetValue<int>("RequestServerLocalPort")}");

            _connection = new HubConnectionBuilder()
                .ConfigureLogging(logging => logging.AddNLogWeb())
                .WithAutomaticReconnect(new SignalrAlwaysRetryPolicy(TimeSpan.FromSeconds(_configuration.GetValue<int>("RetryTimeSecond"))))
                .WithUrl(url.ToString())
                .Build();

            _connection.On<Guid>("CreateSessionAsync", CreateSessionAsync);

            _connection.On<Guid>("DeleteSessionAsync", DeleteSessionAsync);

            _connection.On<Guid, string>("SendData", SendData);

        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _connection.StartAsync();

            _logger.LogInformation($"Connected to server!");
        }



        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _connection.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }



        public async Task CreateSessionAsync(Guid sessionId)
        {

            var hostPort = _configuration.GetValue<int>("ClientSharedLocalPort");

            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, hostPort);

            _listSessionConnect.TryAdd(sessionId, client);

            var hostSocketService = new HostSocketService(
                logger: _logger,
                connection: _connection,
                client: client!,
                sessionId: sessionId
                );

            hostSocketService.HandleHostSocketProxyAsync();

        }



        public Task DeleteSessionAsync(Guid sessionId)
        {

            if (_listSessionConnect.Remove(sessionId, out var currentClient))
            {
                try
                {
                    currentClient?.Close();
                    currentClient?.Dispose();
                }
                catch { };
            };

            return Task.CompletedTask;
        }



        public void SendData(Guid sessionId, string data)
        {

            //_logger.LogInformation($"SendDatasync: {fromUserName} -> {toUserName} {sessionId} {data}");

            _listSessionConnect[sessionId].GetStream().Write(Convert.FromBase64String(data));
        }

    }
}
