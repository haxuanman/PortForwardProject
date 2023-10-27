using FortForwardGatewayClient.Common;
using FortForwardLib.Interface;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Threading.Channels;

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

                _logger.LogError($"HandleClientSocketAsync {ex}");

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
