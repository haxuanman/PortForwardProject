using FortForwardGatewayClient.Common;
using FortForwardLib.Interface;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace FortForwardGatewayClient.Services
{
    internal class HostSocketService : IDisposable
    {

        private readonly ILogger _logger;
        private readonly HubClientConfig _hubClientConfig;
        private readonly HubConnection _connection;
        private readonly TcpClient _client;
        private readonly Guid _sessionId;



        internal HostSocketService(
            ILogger logger,
            HubClientConfig hubClientConfig,
            HubConnection connection,
            TcpClient client,
            Guid sessionId
            )
        {
            _logger = logger;
            _client = client;
            _connection = connection;
            _hubClientConfig = hubClientConfig;
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



        internal async void HandleHostSocketProxyAsync()
        {
            await HandleHostSocketAsync();
        }



        private async Task HandleHostSocketAsync()
        {

            _logger.LogInformation($"New session {_sessionId}");

            try
            {

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
                        }
                        );

                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                _logger.LogInformation($"Close session {_sessionId}");
            }
        }
    }
}
