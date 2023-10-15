using System.Net.Sockets;

namespace PortForwardServer.Services
{
    internal class ClientSockerService : IDisposable
    {

        private readonly ILogger _logger;
        private readonly ISocketServerHub _caller;
        private readonly TcpClient? _client;
        private readonly Guid _sessionId;



        internal ClientSockerService(
            ILogger logger,
            ISocketServerHub caller,
            TcpClient? client,
            Guid sessionId
            )
        {
            _logger = logger;
            _caller = caller;
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

                await _caller.CreateSessionAsync(_sessionId);

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

                    await _caller.SendDatasync(_sessionId, bufferString);

                }
            }
            catch (Exception ex)
            {

                _logger.LogError(ex.ToString());

                await _caller.DeleteSessionAsync(_sessionId);
            }
            finally
            {
                _logger.LogInformation($"Close session {_sessionId}: {_client?.Client?.RemoteEndPoint}");
            }

        }

    }
}
