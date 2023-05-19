﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;
using PortForwardServer.Dto;
using System.Net;
using System.Net.Sockets;

namespace PortForwardServer
{
    public class SocketServerHub : Hub<ISocketServerHub>
    {

        private TcpListener _listener = new TcpListener(IPAddress.Any, 0);
        private string _connectionId = string.Empty;
        private static Dictionary<string, TcpClient> _listChildConnect = new();
        private readonly ILogger<SocketServerHub> _logger;
        private readonly object _lockerSend = new object();
        private readonly object _lockerReviced = new object();



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

                _connectionId = Context?.ConnectionId ?? string.Empty;

                _listener?.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), new HandleIncomingConnectionStateDto
                {
                    Clients = Clients
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }



        private void HandleIncomingConnection(IAsyncResult result)
        {

            var states = result.AsyncState as HandleIncomingConnectionStateDto;

            var client = _listener?.EndAcceptTcpClient(result);

            _listener?.BeginAcceptTcpClient(new AsyncCallback(HandleIncomingConnection), new HandleIncomingConnectionStateDto
            {
                Clients = Clients
            });

            HandleChildClientProxy(client!, states!.Clients!);

        }



        async void HandleChildClientProxy(TcpClient client, IHubCallerClients<ISocketServerHub> clients)
        {
            await HandleChildClient(client, clients);
        }



        async Task HandleChildClient(TcpClient client, IHubCallerClients<ISocketServerHub> clients)
        {
            var childClientName = string.Empty;
            try
            {

                childClientName = ((IPEndPoint?)client?.Client.RemoteEndPoint)?.ToString() ?? string.Empty;

                await clients.Caller.RequestChildClient(childClientName);

                _listChildConnect[childClientName] = client!;

                _logger.LogInformation($"New child client of {_connectionId} connected: {childClientName}");

                var bufferSize = Math.Min(8192, client?.ReceiveBufferSize ?? 8192);

                while (client?.Connected ?? false)
                {

                    var buffer = new byte[bufferSize];

                    var stream = client.GetStream();

                    var byteRead = await stream.ReadAsync(buffer);

                    if (byteRead == 0) continue;

                    buffer = buffer.Take(byteRead).ToArray();

                    var bufferString = Convert.ToBase64String(buffer);

                    //_logger.LogInformation($"Send | {childClientName} | {byteRead} | {bufferString}");

                    //lock (_lockerSend)
                    //{
                    //    clients.Caller.ChildClientSocketRequest(childClientName, bufferString).Wait();
                    //}

                    await clients.Caller.ChildClientSocketRequest(childClientName, bufferString);

                }

            }
            catch (Exception ex)
            {

                _logger.LogInformation($"Child client {childClientName}: {ex.Message}");

                _logger.LogError(ex.ToString());
            }
            finally
            {

                await clients.Caller.CloseChildClient(childClientName);

                _listChildConnect.Remove(childClientName);

                _logger.LogInformation($"Child client of {_connectionId} disconnected: {childClientName}");
            }
        }



        public override async Task OnDisconnectedAsync(Exception? exception)
        {

            _logger.LogInformation($"Client disconnected {Context?.ConnectionId}");

            await base.OnDisconnectedAsync(exception);

            _listener?.Stop();
        }



        [HubMethodName("ChildClientSocketReponse")]
        public void ChildClientSocketReponse(string childClientName, string bufferString)
        {

            try
            {

                var childClient = _listChildConnect[childClientName];

                if (!(childClient?.Connected ?? false)) return;

                lock (_lockerReviced)
                {
                    childClient.GetStream().Write(Convert.FromBase64String(bufferString));
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

        }

    }
}
