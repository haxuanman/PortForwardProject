using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using PortForwardServer.Dal;
using PortForwardServer.Services;
using System.Net;
using System.Net.Sockets;

namespace PortForwardServer
{
    public class SocketServerHub : Hub<ISocketServerHub>
    {

        private TcpListener _listener;
        private static readonly Dictionary<Guid, TcpClient> _listSessionConnect = new();
        private static readonly Dictionary<string, List<Guid>> _listSessionConnection = new();
        private static readonly Dictionary<string, TcpListener> _listConnectionListener = new();
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

                if (!_listConnectionListener.ContainsKey(Context!.ConnectionId)) _listConnectionListener.Add(Context.ConnectionId, new TcpListener(IPAddress.Any, requestLocalPort));

                _listener = _listConnectionListener[Context.ConnectionId];

                _listener.Start();

                _logger.LogInformation($"Open local port {((IPEndPoint?)_listener.LocalEndpoint)?.Port} for client {Context?.ConnectionId}");

                _listener.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), new HandleIncomingConnectionStateDto
                {
                    ConnectionId = Context?.ConnectionId,
                    Caller = Clients.Caller,
                });

            }
            catch (Exception ex)
            {
                _logger.LogError($"OnConnectedAsync: {ex}");
            }

        }



        private void HandleIncomingConnection(IAsyncResult result)
        {

            try
            {
                var state = result.AsyncState as HandleIncomingConnectionStateDto;

                _listConnectionListener.TryGetValue(state!.ConnectionId!, out var listener);
                if (listener == null) return;

                var client = listener.EndAcceptTcpClient(result);

                listener.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), state);

                var sessionId = Guid.NewGuid();

                var connectionId = state!.ConnectionId!;

                _listSessionConnect.TryAdd(sessionId, client!);
                if (!_listSessionConnection.ContainsKey(connectionId)) _listSessionConnection.Add(connectionId, new List<Guid>());
                _listSessionConnection[connectionId].Add(sessionId);

                var clientSockerService = new ClientSockerService(
                    logger: _logger,
                    caller: state!.Caller!,
                    client: client,
                    sessionId: sessionId
                    );

                clientSockerService.HandleClientSocketProxyAsync();
            }
            catch { }

        }



        public override async Task OnDisconnectedAsync(Exception? exception)
        {

            _logger.LogInformation($"Client disconnected {Context?.ConnectionId}");

            if (_listSessionConnection.ContainsKey(Context?.ConnectionId ?? string.Empty))
            {
                foreach (var item in _listSessionConnection[Context?.ConnectionId ?? string.Empty] ?? new List<Guid>())
                {
                    try
                    {
                        _listSessionConnect[item].Close();
                    }
                    catch { }

                    try
                    {
                        _listSessionConnect[item].Dispose();
                    }
                    catch { }

                    try
                    {
                        _listSessionConnect.Remove(item);
                    }
                    catch { }
                }
            }

            try
            {
                _listSessionConnection.Remove(Context?.ConnectionId ?? string.Empty);
            }
            catch { }

            try
            {
                _listConnectionListener.TryGetValue(Context!.ConnectionId, out var listener);
                listener?.Stop();
            }
            catch { }

            await base.OnDisconnectedAsync(exception);

        }



        [HubMethodName("SendDataAsync")]
        public Task SendDataAsync(Guid sessionId, string data)
        {

            //_logger.LogInformation($"SendDatasync: {fromUserName} -> {toUserName} {sessionId} {data}");

            return _listSessionConnect[sessionId].GetStream().WriteAsync(Convert.FromBase64String(data)).AsTask();
        }

    }
}
