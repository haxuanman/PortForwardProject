using CommonService.Dto;
using CommonService.ExtensionClass;
using CommonService.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PortForwardServer
{
    public class SocketServer : IHostedService
    {

        private readonly Socket _listenerPublic = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly BlockingCollection<ItemClientRequestInfo> _listPublicClients = new();
        private readonly IConfiguration _configuration = HelperConfiguration.GetConfiguration();
        private readonly object _lock = new();



        public async void ListenCallbackLocal(IAsyncResult result)
        {

            var client = (Socket)null;
            var remoteClient = (ItemClientRequestInfo)null;
            var serverChildClient = (ItemClientRequestInfo)null;

            try
            {

                var listener = result.AsyncState as Socket;
                if (listener == null) return;

                client = listener.EndAccept(result);
                if (client == null || client.RemoteEndPoint == null) return;

                var clientName = client.RemoteEndPoint.ToString();

                #region tạo child client từ client

                var requestChildClient = new RequestNewChildrenClientDto
                {
                    ServerChildrentEnpoint = client?.RemoteEndPoint?.ToString() ?? string.Empty,
                    ServerChildrentPort = ((IPEndPoint)client?.RemoteEndPoint!).Port,

                    ServerPort = ((IPEndPoint)client.LocalEndPoint!).Port,
                };

                var messageNewChildClient = HelperClientServerMessage.CreateMessageObject(
                    (int)ConstClientServerMessageType.RequestNewChildClient,
                    JsonConvert.SerializeObject(requestChildClient));


                remoteClient = _listPublicClients.Where(e => e.ServerPort == ((IPEndPoint)client.LocalEndPoint).Port).FirstOrDefault();
                if (remoteClient == null) return;
                await remoteClient.CurrentClient.SendAsync(HelperClientServerMessage.GetMessageBytes(messageNewChildClient).ToArray(), SocketFlags.None);

                Console.WriteLine($"New Child client {clientName} connect to server {listener.LocalEndPoint}");

                serverChildClient = new ItemClientRequestInfo
                {
                    CurrentClient = client,

                    ServerPort = requestChildClient.ServerPort,
                    ServerEndpointName = requestChildClient.ServerChildrentEnpoint,
                };
                remoteClient.ChildrenClients.Add(serverChildClient);

                #endregion

                listener.BeginAccept(new AsyncCallback(ListenCallbackLocal), listener);

                #region nhận data
                try
                {

                    while (true)
                    {
                        if (!client.IsConnected())
                        {
                            Console.WriteLine($"Client {client.LocalEndPoint} disconnected to server {client.RemoteEndPoint}");
                            break;
                        }

                        var state = new ReadCallbackStateObject(client);

                        var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                        state.SaveMessageBuffer(read);

                        if (state.revicedBytes.Count > 0)
                        {
                            var messageData = HelperClientServerMessage.CreateMessageObject(
                            (int)ConstClientServerMessageType.Default,
                            requestChildClient.ServerChildrentEnpoint,
                            state.revicedBytes);

                            lock (_lock)
                            {
                                HelperClientServerMessage.SendMessageAsync(remoteClient.CurrentClient, messageData);
                            }

                        }

                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
                finally { }

                #endregion
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally
            {
                if (remoteClient != null && serverChildClient != null)
                {
                    serverChildClient.CurrentClient.SafeClose();
                    remoteClient.ChildrenClients.Remove(serverChildClient);
                }
            }

        }



        public async void ListenCallbackPublic(IAsyncResult result)
        {

            var client = (Socket)null;
            var listenerLocal = (Socket)null;
            var clientInfo = (ItemClientRequestInfo)null;

            try
            {
                var listener = result.AsyncState as Socket;
                if (listener == null) return;
                client = listener.EndAccept(result);
                if (client.RemoteEndPoint == null) return;

                var clientName = client.RemoteEndPoint.ToString();

                #region tạo port cho client
                var requestNewPortMgsBytes = new byte[client.ReceiveBufferSize];
                int reviceByte = await client.ReceiveAsync(requestNewPortMgsBytes, SocketFlags.None);
                var requestNewPortMgs = HelperClientServerMessage.GetMessageObject(requestNewPortMgsBytes.ToList().Take(reviceByte).ToList());
                var requestNewPortInfo = JsonConvert.DeserializeObject<ClientRequestNewPortDto>(requestNewPortMgs.MessageData);

                var listenerLocalEndPoint = new IPEndPoint(IPAddress.Any, requestNewPortInfo.RequestPort);
                listenerLocal = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                listenerLocal.Bind(listenerLocalEndPoint);
                listenerLocal.Listen();
                Console.WriteLine($"Created listen port {((IPEndPoint)listenerLocal.LocalEndPoint).Port} for client {client.RemoteEndPoint}");

                clientInfo = new ItemClientRequestInfo
                {
                    ClientPort = requestNewPortInfo.LocalPort,
                    ClientEndpointName = client.RemoteEndPoint.ToString(),

                    ServerEndpointName = listenerLocal.LocalEndPoint.ToString(),
                    ServerPort = ((IPEndPoint)listenerLocal.LocalEndPoint).Port,

                    CurrentClient = client
                };

                _listPublicClients.TryAdd(clientInfo);

                listenerLocal.BeginAccept(new AsyncCallback(ListenCallbackLocal), listenerLocal);

                #endregion

                #region trả reponse cho client
                var requestNewPort = new ClientRequestNewPortDto
                {
                    LocalPort = requestNewPortInfo.LocalPort,
                    RequestPort = clientInfo.ServerPort
                };
                var requestNewPortMgsResponse = HelperClientServerMessage.CreateMessageObject(
                    (int)ConstClientServerMessageType.RequestNewClient,
                    JsonConvert.SerializeObject(requestNewPort));
                await client.SendAsync(HelperClientServerMessage.GetMessageBytes(requestNewPortMgs).ToArray(), SocketFlags.None);
                #endregion

                Console.WriteLine($"Client {client.RemoteEndPoint} connected to server {listener.LocalEndPoint}");

                listener.BeginAccept(new AsyncCallback(ListenCallbackPublic), listener);

                string messageQueue = string.Empty;

                #region lắng nghe gói tin
                try
                {
                    while (true)
                    {
                        if (!client.IsConnected())
                        {
                            Console.WriteLine($"Client {client.LocalEndPoint} disconnected to server {client.RemoteEndPoint}");
                            break;
                        }

                        var state = new ReadCallbackStateObject(client);

                        var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                        state.SaveMessageBuffer(read);

                        messageQueue += Encoding.UTF8.GetString(state.revicedBytes.ToArray());

                        var listMessageData = new List<ClientServerMessageDto>();

                        lock (_lock)
                        {
                            listMessageData = HelperClientServerMessage.GetListMessageObject(ref messageQueue);
                        }

                        foreach (var messageData in listMessageData)
                        {

                            switch (messageData.MessageType)
                            {
                                case (int)ConstClientServerMessageType.Default:

                                    var clientRemoteInfo = _listPublicClients
                                        .Where(e => e.ClientEndpointName == client.RemoteEndPoint.ToString())
                                        .FirstOrDefault();
                                    if (clientRemoteInfo == null) break;

                                    var sendToChildClient = clientRemoteInfo.ChildrenClients.Where(e => e.ServerEndpointName == messageData.ServerChildEndPoint)
                                        .FirstOrDefault();
                                    if (sendToChildClient == null) break;

                                    lock (_lock)
                                    {
                                        sendToChildClient.CurrentClient.Send(Convert.FromBase64String(messageData.MessageData), SocketFlags.None);
                                    }

                                    break;
                                case (int)ConstClientServerMessageType.RequestNewClient:
                                    break;
                                case (int)ConstClientServerMessageType.RequestNewChildClient:
                                    break;

                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }
                finally { }
                #endregion
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally
            {
                client.SafeClose();
                listenerLocal.SafeClose();

                _listPublicClients.TryTake(out clientInfo);
                if (clientInfo != null) clientInfo.Dispose();
            }
        }



        public Task StartAsync(CancellationToken cancellationToken)
        {
            var listenerPublicEndPoint = new IPEndPoint(IPAddress.Any, _configuration.GetValue<int>("Server:PublicPort"));
            _listenerPublic.Bind(listenerPublicEndPoint);
            _listenerPublic.Listen();
            _listenerPublic.BeginAccept(new AsyncCallback(ListenCallbackPublic), _listenerPublic);

            return Task.CompletedTask;
        }



        public Task StopAsync(CancellationToken cancellationToken)
        {
            _listenerPublic.SafeClose();
            return Task.CompletedTask;
        }
    }
}
