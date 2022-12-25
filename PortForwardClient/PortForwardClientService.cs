using CommonService.Dto;
using CommonService.ExtensionClass;
using CommonService.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using PortForwardClient.Dto;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PortForwardClient
{
    public class PortForwardClientService : IHostedService
    {

        private readonly Socket _localClient = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly Socket _remoteClient = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly BlockingCollection<ItemClientRequestInfo> _listLocalChildrenClients = new();
        private readonly IConfiguration _configuration = HelperConfiguration.GetConfiguration();
        private readonly object _lock = new();
        
        
        private async void ConnectCallbackRemoteClient(IAsyncResult ar)
        {

            Socket client  = null;

            try
            {
                client = (Socket)ar.AsyncState;
                client.EndConnect(ar);

                #region request remote port
                var requestNewPort = new ClientRequestNewPortDto
                {
                    LocalPort = _configuration.GetValue<int>("Client:LocalPort"),
                    RequestPort = _configuration.GetValue<int?>("Client:RequestPort").GetValueOrDefault(0)
                };
                var requestNewPortMgs = HelperClientServerMessage.CreateMessageObject(
                    (int)ConstClientServerMessageType.RequestNewClient,
                    JsonConvert.SerializeObject(requestNewPort));
                var messageByte = HelperClientServerMessage.GetMessageBytes(requestNewPortMgs);

                client.Send(messageByte.ToArray(), SocketFlags.None);
                #endregion

                #region revice reponse
                var requestNewPortMgsResponseBytes = new byte[client.ReceiveBufferSize];
                int reviceByte = await client.ReceiveAsync(requestNewPortMgsResponseBytes, SocketFlags.None);
                var requestNewPortResponseMgs = HelperClientServerMessage.GetMessageObject(requestNewPortMgsResponseBytes.ToList().Take(reviceByte).ToList());
                var requestNewPortInfo = JsonConvert.DeserializeObject<ClientRequestNewPortDto>(requestNewPortResponseMgs.MessageData) ?? new();

                Console.WriteLine($"Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint} with remote port {requestNewPortInfo.RequestPort}");
                #endregion

                #region lắng nghe gói tin

                string messageQueue = string.Empty;

                try
                {
                    while (true)
                    {
                        if (!client.IsConnected())
                        {
                            Console.WriteLine($"Client {client.LocalEndPoint} disconnected to server {client.RemoteEndPoint}");
                            break;
                        }

                        var state = new ClientReadCallbackStateObject(client);

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

                                    var localChildClient = _listLocalChildrenClients
                                        .Where(e => e.ServerEndpointName == messageData.ServerChildEndPoint)
                                        .FirstOrDefault();
                                    if (localChildClient?.CurrentClient == null) break;

                                    localChildClient.CurrentClient.Send(Convert.FromBase64String(messageData.MessageData), SocketFlags.None);

                                    break;

                                case (int)ConstClientServerMessageType.RequestNewClient:
                                    break;

                                case (int)ConstClientServerMessageType.RequestNewChildClient:

                                    var childClientEndPoint = new IPEndPoint(IPAddress.Loopback, _configuration.GetValue<int>("Client:LocalPort"));
                                    var childClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                                    var messageNewChildClient = JsonConvert.DeserializeObject<RequestNewChildrenClientDto>(messageData.MessageData);
                                    if (messageNewChildClient == null) break;


                                    var newChildClientState = new ItemClientRequestInfo
                                    {
                                        CurrentClient = childClient,

                                        ServerEndpointName = messageNewChildClient.ServerChildrentEnpoint,
                                        ServerPort = messageNewChildClient.ServerPort
                                    };
                                    childClient.BeginConnect(childClientEndPoint, new AsyncCallback(ConnectCallbackLocalClient), newChildClientState);

                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }

                #endregion
            }
            catch (Exception ex) { Console.WriteLine(ex); throw; }
            finally
            {
                client.SafeClose();
            }
        }



        private async void ConnectCallbackLocalClient(IAsyncResult ar)
        {
            var clientInfo = (ItemClientRequestInfo)null;
            var client = (Socket)null;

            try
            {
                clientInfo = ar.AsyncState as ItemClientRequestInfo;
                if (clientInfo == null) return;
                client = clientInfo.CurrentClient;
                client.EndConnect(ar);

                clientInfo.ClientEndpointName = client.LocalEndPoint.ToString();
                clientInfo.ClientPort = ((IPEndPoint)client.LocalEndPoint).Port;
                _listLocalChildrenClients.TryAdd(clientInfo);

                Console.WriteLine($"New Child client {client.RemoteEndPoint} connect to server {client.LocalEndPoint}");

                #region lắng nghe gói tin

                try
                {
                    while (true)
                    {
                        if (!client.IsConnected())
                        {
                            Console.WriteLine($"Client {client.RemoteEndPoint} disconnected to server {client.LocalEndPoint}");
                            break;
                        }

                        var state = new ClientReadCallbackStateObject(client);

                        var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                        state.SaveMessageBuffer(read);

                        if (state.revicedBytes.Count > 0)
                        {
                            var messageData = HelperClientServerMessage.CreateMessageObject(
                            (int)ConstClientServerMessageType.Default,
                            clientInfo.ServerEndpointName,
                            state.revicedBytes);

                            lock (_lock)
                            {
                                HelperClientServerMessage.SendMessageAsync(_remoteClient, messageData);
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex); }

                #endregion
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally
            {
                client.SafeClose();
                _listLocalChildrenClients.TryTake(out clientInfo);
            }
        }



        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Strarting Server");

            var localEndPoint = new IPEndPoint(IPAddress.Loopback, _configuration.GetValue<int>("Client:LocalPort"));

            var remoteIp = Dns.Resolve(_configuration.GetValue<string>("Server:PublicAddress")).AddressList.Select(e => e.MapToIPv4()).First();
            var remoteEndPoint = new IPEndPoint(remoteIp, _configuration.GetValue<int>("Server:PublicPort"));

            _remoteClient.BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallbackRemoteClient), _remoteClient);

            Console.WriteLine("Strarted Server");
            return Task.CompletedTask;
        }



        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stoping Server");

            _remoteClient.SafeClose();
            _localClient.SafeClose();

            Console.WriteLine("Stopted Server");
            return Task.CompletedTask;
        }
    }
}
