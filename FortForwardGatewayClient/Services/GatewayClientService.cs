using FortForwardGatewayClient.Common;
using FortForwardLib.Interface;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace FortForwardGatewayClient.Services
{
    public class GatewayClientService : IHostedService, IAsyncDisposable, IPortForwardHubClientMethod
    {

        private TcpListener _listener = new TcpListener(IPAddress.Any, 0);


        private readonly HubConnection _connection;
        private readonly static ConcurrentDictionary<Guid, TcpClient> _listSessionConnect = new();

        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly HubClientConfig _hubClientConfig;



        public GatewayClientService(
            IConfiguration configuration,
            ILogger<GatewayClientService> logger,
            HubClientConfig hubClientConfig
            )
        {

            _logger = logger;

            _configuration = configuration;

            _hubClientConfig = hubClientConfig;

            var url = new UriBuilder($"{configuration.GetValue<string>("ServerUrl")}/PortForwardServerHub");

            var query = new QueryBuilder();
            query.Add(nameof(hubClientConfig.UserName), hubClientConfig.UserName ?? string.Empty);
            query.Add(nameof(hubClientConfig.IsClient), hubClientConfig.IsClient?.ToString() ?? string.Empty);
            query.Add(nameof(hubClientConfig.HostPort), hubClientConfig.HostPort?.ToString() ?? string.Empty);
            query.Add(nameof(hubClientConfig.ClientPort), hubClientConfig.ClientPort?.ToString() ?? string.Empty);

            url.Query = query.ToString();

            _connection = new HubConnectionBuilder()
                .ConfigureLogging(logging => logging.AddNLogWeb())
                .WithAutomaticReconnect(new SignalrAlwaysRetryPolicy(TimeSpan.FromSeconds(_configuration.GetValue<int>("RetryTimeSecond"))))
                .WithUrl(url.ToString())
            .Build();

            _connection.On<string, string, Guid, int>(nameof(IPortForwardHubClientMethod.CreateSessionAsync), CreateSessionAsync);
            _connection.On<string, string, Guid>(nameof(IPortForwardHubClientMethod.DeleteSessionAsync), DeleteSessionAsync);
            _connection.On<string, string, Guid, string>(nameof(IPortForwardHubClientMethod.SendDataAsync), SendDataAsync);

        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {

            await _connection.StartAsync(cancellationToken);

            _logger.LogInformation($"Connected to server!");

            if (_hubClientConfig.IsClient == true)
            {

                var clientPort = _hubClientConfig?.ClientPort.GetValueOrDefault() ?? throw new Exception("ConnectPort is null");

                _listener = new TcpListener(IPAddress.Any, clientPort);

                _listener?.Start();

                _listener?.BeginAcceptTcpClient(new AsyncCallback(OnAcceptTcpClient), null);

                _logger.LogInformation($"Start local port {clientPort}!");

            }
        }



        private void OnAcceptTcpClient(IAsyncResult result)
        {

            var client = _listener?.EndAcceptTcpClient(result);

            _listener?.BeginAcceptTcpClient(new AsyncCallback(OnAcceptTcpClient), null);

            var sessionId = Guid.NewGuid();

            _listSessionConnect.TryAdd(sessionId, client);

            var clientSockerService = new ClientSockerService(
                logger: _logger,
                hubClientConfig: _hubClientConfig,
                connection: _connection,
                client: client,
                sessionId: sessionId);

            clientSockerService.HandleClientSocketProxyAsync();

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



        public async ValueTask DisposeAsync()
        {
            try
            {
                _listener.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            try
            {
                await _connection.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }



        public Task SendDataAsync(string fromUserName, string toUserName, Guid sessionId, string data)
        {

            //_logger.LogInformation($"SendDatasync: {fromUserName} -> {toUserName} {sessionId} {data}");

            _listSessionConnect[sessionId].GetStream().Write(Convert.FromBase64String(data));

            return Task.CompletedTask;

        }



        public async Task CreateSessionAsync(string fromUserName, string toUserName, Guid sessionId, int hostPort)
        {

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(IPAddress.Loopback, hostPort);

            _listSessionConnect.TryAdd(sessionId, tcpClient);

            var hostSocketService = new HostSocketService(
                logger: _logger,
                hubClientConfig: new HubClientConfig
                {
                    UserName = toUserName,
                    HostUserName = fromUserName
                },
                connection: _connection,
                client: tcpClient,
                sessionId: sessionId
                );

            hostSocketService.HandleHostSocketProxyAsync();

        }



        public Task DeleteSessionAsync(string hostUserName, string clientUserName, Guid sessionId)
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

    }
}
