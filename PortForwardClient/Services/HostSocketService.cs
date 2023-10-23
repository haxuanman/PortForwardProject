using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Threading.Channels;

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

                const string sendMethod = "SendDataAsync";

                var buffer = new byte[16384];

                var channel = Channel.CreateUnbounded<byte[]>();

                var stream = _client.GetStream();

                await _connection.SendCoreAsync("StreamDataAsync",
                    new object[]
                    {
                        _sessionId,
                        channel.Reader
                    });

                while (_client?.Connected ?? false)
                {

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    await channel.Writer.WriteAsync(buffer[..byteRead]);

                }

                channel.Writer.Complete();

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
