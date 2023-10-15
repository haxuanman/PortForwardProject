using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using PortForwardServer.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace PortForwardServer
{
    public class SocketServerHub : Hub<ISocketServerHub>
    {

        private TcpListener _listener = new TcpListener(IPAddress.Any, 0);
        private static ConcurrentDictionary<Guid, TcpClient> _listSessionConnect = new();
        private readonly ILogger _logger;



        public SocketServerHub(ILogger<SocketServerHub> logger)
        {
            _logger = logger;
        }



        public override async Task OnConnectedAsync()
        {

            try
            {

                await base.OnConnectedAsync();

                var requestLocalPortQuery = new StringValues();

                Context?.GetHttpContext()?.Request.Query.TryGetValue("RequestServerLocalPort", out requestLocalPortQuery);

                var requestLocalPort = Convert.ToInt32(requestLocalPortQuery.FirstOrDefault(string.Empty));

                _logger.LogInformation($"New client connect {Context?.ConnectionId}");

                _listener = new TcpListener(IPAddress.Any, requestLocalPort);

                _listener?.Start();

                _logger.LogInformation($"Open local port {((IPEndPoint?)_listener?.LocalEndpoint)?.Port} for client {Context?.ConnectionId}");

                _listener?.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), Clients.Caller);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }



        private void HandleIncomingConnection(IAsyncResult result)
        {

            var caller = result.AsyncState as ISocketServerHub;

            var client = _listener?.EndAcceptTcpClient(result);

            _listener?.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), caller);

            var sessionId = Guid.NewGuid();

            _listSessionConnect.TryAdd(sessionId, client!);

            var clientSockerService = new ClientSockerService(
                logger: _logger,
                caller: caller,
                client: client,
                sessionId: sessionId
                );

            clientSockerService.HandleClientSocketProxyAsync();

        }



        public override async Task OnDisconnectedAsync(Exception? exception)
        {

            _logger.LogInformation($"Client disconnected {Context?.ConnectionId}");

            await base.OnDisconnectedAsync(exception);

            _listener?.Stop();
        }



        [HubMethodName("SendDatasync")]
        public async Task SendDatasync(Guid sessionId, string data)
        {

            //_logger.LogInformation($"SendDatasync: {fromUserName} -> {toUserName} {sessionId} {data}");

            var client = _listSessionConnect.GetValueOrDefault(sessionId) ?? throw new Exception($"Client {sessionId} is null");


            await client.GetStream().WriteAsync(Convert.FromBase64String(data));
        }

    }
}
