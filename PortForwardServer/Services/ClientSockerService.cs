using System.Net.Sockets;
using System;

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

                var buffer = new byte[8192];

                while (_client?.Connected ?? false)
                {

                    var byteRead = await _client.GetStream().ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    _logger.LogError($"HandleClientSocketAsync: {Convert.ToBase64String(buffer[..byteRead].ToArray())}");

                    await _caller.SendDataAsync(_sessionId, Convert.ToBase64String(buffer[..byteRead].ToArray()));

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
