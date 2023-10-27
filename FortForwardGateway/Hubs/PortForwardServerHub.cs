using FortForwardGateway.Dal;
using FortForwardLib.Interface;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace FortForwardGateway.Hubs
{
    public class PortForwardServerHub : Hub<IPortForwardHubClientMethod>
    {

        private readonly ILogger _logger;



        private static ConcurrentDictionary<string, HubClientData> ListUsers { get; set; } = new ConcurrentDictionary<string, HubClientData>();



        public PortForwardServerHub(ILogger<PortForwardServerHub> logger)
        {
            _logger = logger;
        }



        public override Task OnConnectedAsync()
        {

            if (Context == null) throw new Exception("Context is null");

            var userName = Context?.GetHttpContext()?.Request.Query["userName"].ToString()?.ToLower() ?? string.Empty;
            var type = Context?.GetHttpContext()?.Request.Query["type"].ToString()?.ToLower() ?? string.Empty;
            var connectPortStr = Context?.GetHttpContext()?.Request.Query["ConnectPort"].ToString()?.ToLower() ?? string.Empty;
            var sharedPortStr = Context?.GetHttpContext()?.Request.Query["SharedPort"].ToString()?.ToLower() ?? string.Empty;

            var connectPort = (int?)null;
            if (int.TryParse(connectPortStr, out var connectPortParse)) connectPort = connectPortParse;

            var sharedPort = (int?)null;
            if (int.TryParse(sharedPortStr, out var sharedPortParse)) sharedPort = sharedPortParse;

            if (ListUsers.ContainsKey(userName)) throw new Exception("userName exites");

            ListUsers.TryAdd(userName, new HubClientData
            {
                ConnectionId = Context?.ConnectionId,
                UserName = userName,
                Type = type,
                ConnectPort = connectPort,
                SharedPort = sharedPort,
            });

            _logger.LogInformation($"New client {Context?.ConnectionId}");

            base.OnConnectedAsync();

            return Task.CompletedTask;

        }



        public override Task OnDisconnectedAsync(Exception? exception)
        {

            _logger.LogInformation($"Close client {Context?.ConnectionId}: {exception}");

            var userName = Context?.GetHttpContext()?.Request.Query["userName"].ToString()?.ToLower() ?? string.Empty;

            ListUsers.Remove(userName, out _);

            base.OnDisconnectedAsync(exception);

            return Task.CompletedTask;

        }



        public Task CreateSessionAsync(string fromUserName, string toUserName, Guid sessionId, int hostPort)
        {

            _logger.LogInformation($"CreateSessionAsync {fromUserName} -> {toUserName} {sessionId} {hostPort}");

            if (ListUsers.TryGetValue(toUserName, out var toUserNameClient))
            {
                return Clients.Client(toUserNameClient.ConnectionId ?? string.Empty).CreateSessionAsync(fromUserName, toUserName, sessionId, hostPort);
            }

            return Task.CompletedTask;

        }



        public Task DeleteSessionAsync(string fromUserName, string toUserName, Guid sessionId)
        {

            _logger.LogInformation($"DeleteSessionAsync {fromUserName} -> {toUserName} {sessionId}");

            if (ListUsers.TryGetValue(toUserName, out var toUserNameClient))
            {
                return Clients.Client(toUserNameClient.ConnectionId ?? string.Empty).DeleteSessionAsync(fromUserName, toUserName, sessionId);
            }

            return Task.CompletedTask;

        }



        [HubMethodName("StreamDataAsync")]
        public async Task StreamDataAsync(string fromUserName, string toUserName, Guid sessionId, IAsyncEnumerable<byte[]> dataStream)
        {

            _logger.LogInformation($"StreamDataAsync {fromUserName} -> {toUserName} {sessionId}");

            if (ListUsers.TryGetValue(toUserName, out var toUserNameClient))
            {

                await foreach (var data in dataStream)
                {
                    await Clients.Client(toUserNameClient.ConnectionId ?? string.Empty).SendDataByteAsync(
                        fromUserName: fromUserName,
                        toUserName: toUserName,
                        sessionId: sessionId,
                        data: data
                        );
                }
            }

        }

    }
}
