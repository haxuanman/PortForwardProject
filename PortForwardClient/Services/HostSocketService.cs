using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System;

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

                var buffer = new Memory<byte>();

                while (_client?.Connected ?? false)
                {

                    var byteRead = await _client.GetStream().ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    await _connection.SendCoreAsync(
                        "SendDatasync",
                        new object[]
                        {
                            _sessionId,
                            Convert.ToBase64String(buffer[..byteRead].ToArray())
                        });

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
