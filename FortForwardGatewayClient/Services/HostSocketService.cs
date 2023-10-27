using FortForwardGatewayClient.Common;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Threading.Channels;

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

                string hubClientConfigUserName = _hubClientConfig.UserName ?? string.Empty;
                string hubClientConfigHostUserName = _hubClientConfig.HostUserName ?? string.Empty;

                const string streamHubName = "StreamDataAsync";

                var channel = Channel.CreateUnbounded<byte[]>();

                await _connection.SendCoreAsync(
                        streamHubName,
                        new object[]
                        {
                            hubClientConfigUserName,
                            hubClientConfigHostUserName,
                            _sessionId,
                            channel.Reader
                        });

                var buffer = new byte[32768];

                var stream = _client.GetStream();

                while (_client?.Connected ?? false)
                {

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    await channel.Writer.WriteAsync(buffer[..byteRead]);

                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"HandleHostSocketAsync {ex}");
            }
            finally
            {
                _logger.LogInformation($"Close session {_sessionId}");
            }
        }
    }
}
