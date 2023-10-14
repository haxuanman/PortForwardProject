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
            catch { };
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

                await _connection.InvokeAsync(
                    nameof(IPortForwardHubClientMethod.CreateSessionAsync),
                    _hubClientConfig.UserName,
                    _hubClientConfig.HostUserName,
                    _sessionId,
                    _hubClientConfig.HostPort);

                var bufferSize = Math.Min(8192, _client?.ReceiveBufferSize ?? 8192);

                while (_client?.Connected ?? false)
                {

                    var buffer = new byte[bufferSize];

                    var stream = _client.GetStream();

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    buffer = buffer.Take(byteRead).ToArray();
                    var bufferString = Convert.ToBase64String(buffer);

                    //_logger.LogInformation($"{_hubClientConfig.UserName} -> {_hubClientConfig.HostUserName} {_sessionId}: {bufferString}");

                    await _connection.InvokeAsync(
                        nameof(IPortForwardHubClientMethod.SendDatasync),
                        _hubClientConfig.UserName,
                        _hubClientConfig.HostUserName,
                        _sessionId,
                        bufferString);

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
