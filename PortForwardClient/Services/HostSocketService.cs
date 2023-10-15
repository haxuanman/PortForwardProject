using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace PortForwardClient.Services
{
    internal class HostSocketService : IDisposable
    {

        private readonly ILogger _logger;
        private readonly HubConnection _connection;
        private readonly TcpClient _client;
        private readonly Guid _sessionId;



        internal HostSocketService(
            ILogger logger,
            HubConnection connection,
            TcpClient client,
            Guid sessionId
            )
        {
            _logger = logger;
            _connection = connection;
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



        internal async void HandleHostSocketProxyAsync()
        {
            await HandleHostSocketAsync();
        }



        private async Task HandleHostSocketAsync()
        {

            _logger.LogInformation($"New session {_sessionId}");

            try
            {

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

                    await _connection.SendAsync(
                        "SendDatasync",
                        _sessionId,
                        bufferString);

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
