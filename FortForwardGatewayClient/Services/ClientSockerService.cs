using FortForwardGatewayClient.Common;
using FortForwardLib.Interface;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace FortForwardGatewayClient.Services
{
    internal class ClientSockerService : IDisposable
    {

        private readonly ILogger _logger;
        private readonly HubConnection _connection;
        private readonly HubClientConfig _hubClientConfig;
        private readonly TcpClient? _client;
        private readonly Guid _sessionId;



        internal ClientSockerService(
            ILogger logger,
            HubClientConfig hubClientConfig,
            HubConnection connection,
            TcpClient? client,
            Guid sessionId
            )
        {
            _logger = logger;
            _connection = connection;
            _hubClientConfig = hubClientConfig;
            _client = client;
            _sessionId = sessionId;
        }



        public void Dispose()
        {
            try
            {
                _client?.Dispose();
            }
            catch { }
        }



        internal async void HandleClientSocketProxyAsync()
        {
            await HandleClientSocketAsync();
        }



        private async Task HandleClientSocketAsync()
        {

            _logger.LogInformation($"New session {_sessionId}: {_client?.Client.RemoteEndPoint}");
            try
            {

                await _connection.InvokeCoreAsync(
                    nameof(IPortForwardHubClientMethod.CreateSessionAsync),
                    new object[]
                    {
                        _hubClientConfig.UserName ?? string.Empty,
                        _hubClientConfig.HostUserName ?? string.Empty,
                        _sessionId,
                        _hubClientConfig.HostPort.GetValueOrDefault()
                    });

                await Task.Delay(TimeSpan.FromSeconds(5));

                const string sendMethod = nameof(IPortForwardHubClientMethod.SendDataAsync);
                string hubClientConfigUserName = _hubClientConfig.UserName ?? string.Empty;
                string hubClientConfigHostUserName = _hubClientConfig.HostUserName ?? string.Empty;

                var buffer = new byte[28688];

                while (_client?.Connected ?? false)
                {

                    var byteRead = await _client.GetStream().ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    await _connection.SendCoreAsync(
                        sendMethod,
                        new object[]
                        {
                            hubClientConfigUserName,
                            hubClientConfigHostUserName,
                            _sessionId,
                            Convert.ToBase64String(buffer[..byteRead].ToArray())
                        });

                }
            }
            catch (Exception ex)
            {

                _logger.LogError(ex.ToString());

                await _connection.InvokeAsync(
                    nameof(IPortForwardHubClientMethod.DeleteSessionAsync),
                    _hubClientConfig.UserName,
                    _hubClientConfig.HostUserName,
                    _sessionId);
            }
            finally
            {
                _logger.LogInformation($"Close session {_sessionId}: {_client?.Client?.RemoteEndPoint}");
            }

        }

    }
}
