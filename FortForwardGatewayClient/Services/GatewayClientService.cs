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
            _connection.On<string, string, Guid, byte[]>(nameof(IPortForwardHubClientMethod.SendDataByteAsync), SendDataByteAsync);

        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {

            try
            {
                await _connection.StartAsync(cancellationToken);

                _logger.LogInformation($"Connected to server!");

                if (_hubClientConfig.IsClient == true)
                {

                    var clientPort = _hubClientConfig?.ClientPort.GetValueOrDefault() ?? throw new Exception("ConnectPort is null");

                    _listener = new TcpListener(IPAddress.Any, clientPort);

                    _listener.Start();

                    _listener.BeginAcceptTcpClient(new AsyncCallback(OnAcceptTcpClient), null);

                    _logger.LogInformation($"Start local port {clientPort}!");

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"StartAsync: {ex}");
            }
        }



        private void OnAcceptTcpClient(IAsyncResult result)
        {

            try
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
            catch (Exception ex)
            {
                _logger.LogError($"OnAcceptTcpClient: {ex}");
            }

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



        public async Task CreateSessionAsync(string fromUserName, string toUserName, Guid sessionId, int hostPort)
        {

            try
            {

                _logger.LogInformation($"CreateSessionAsync: {fromUserName} -> {toUserName} {sessionId} {hostPort}");

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
            catch (Exception ex)
            {
                _logger.LogError($"CreateSessionAsync: {ex}");
            }

        }



        public Task DeleteSessionAsync(string fromUserName, string toUserName, Guid sessionId)
        {

            _logger.LogInformation($"DeleteSessionAsync: {fromUserName} -> {toUserName} {sessionId}");

            if (_listSessionConnect.Remove(sessionId, out var currentClient))
            {
                try
                {
                    currentClient?.Close();
                    currentClient?.Dispose();
                }
                catch { }
            }

            return Task.CompletedTask;
        }



        public Task SendDataByteAsync(string fromUserName, string toUserName, Guid sessionId, byte[] data)
        {
            return _listSessionConnect[sessionId].GetStream().WriteAsync(data).AsTask();
        }

    }
}
