using CommonService.Dto;
using CommonService.ExtensionClass;
using CommonService.Helpers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PortForwardClient.Dto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PortForwardClient
{
    public class PortForwardClientService
    {

        private readonly Socket _localClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly Socket _remoteClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly BlockingCollection<ItemClientRequestInfo> _listLocalChildrenClients = new();
        private readonly IConfiguration _configuration = HelperConfiguration.GetConfiguration();
        private readonly object _lock = new object();



        public void Stop()
        {
            try
            {
                _remoteClient.Shutdown(SocketShutdown.Both);
                _localClient.Shutdown(SocketShutdown.Both);
            }
            catch { }
            finally
            {
                _remoteClient.Close();
                _localClient.Close();
            }
        }



        public async Task Send(string data)
        {
            await _localClient.SendAsync(Encoding.UTF8.GetBytes(data), SocketFlags.None);
        }




        public void Start()
        {

            var configuration = HelperConfiguration.GetConfiguration();

            var localEndPoint = new IPEndPoint(IPAddress.Loopback, configuration.GetValue<int>("Client:LocalPort"));

            var remoteIp = Dns.Resolve(configuration.GetValue<string>("Server:PublicAddress")).AddressList.Select(e => e.MapToIPv4()).First();
            var remoteEndPoint = new IPEndPoint(remoteIp, configuration.GetValue<int>("Server:PublicPort"));
            
            _remoteClient.BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallbackRemoteClient), _remoteClient);
        }



        private async void ConnectCallbackRemoteClient(IAsyncResult ar)
        {
            var client = ar.AsyncState as Socket;
            if (client == null) return;
            client.EndConnect(ar);

            Console.WriteLine($"Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint}");

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
            var requestNewPortInfo = JsonConvert.DeserializeObject<ClientRequestNewPortDto>(requestNewPortResponseMgs.MessageData);

            Console.WriteLine($"Connected to server with remote port {requestNewPortInfo.RequestPort}");
            #endregion

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

                    Console.WriteLine($"Has comming message from server {client.RemoteEndPoint} to client {client.LocalEndPoint}: {read} bytes");

                    state.SaveMessageBuffer(read);
                    
                    var messageData = HelperClientServerMessage.GetMessageObject(state.revicedBytes);

                    Console.WriteLine($"Client {client.LocalEndPoint} revice message: {messageData.MessageData}");

                    switch (messageData.MessageType)
                    {
                        case (int)ConstClientServerMessageType.Default:
                            
                            var localChildClient = _listLocalChildrenClients
                                .Where(e => e.ServerEndpointName == messageData.ServerChildEndPoint)
                                .FirstOrDefault();
                            if (localChildClient?.CurrentClient == null) break;
                            lock (_lock)
                            {
                                localChildClient.CurrentClient.Send(Convert.FromBase64String(messageData.MessageData), SocketFlags.None);
                            }
                            

                            break;

                        case (int)ConstClientServerMessageType.RequestNewClient:
                            break;

                        case (int)ConstClientServerMessageType.RequestNewChildClient:

                            var childClientEndPoint = new IPEndPoint(IPAddress.Loopback, _configuration.GetValue<int>("Client:LocalPort"));
                            var childClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                            var messageNewChildClient = JsonConvert.DeserializeObject<RequestNewChildrenClientDto>(messageData.MessageData);


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
            catch (Exception ex) { Console.WriteLine(ex); }
        }



        private async void ConnectCallbackLocalClient(IAsyncResult ar)
        {
            var clientInfo = ar.AsyncState as ItemClientRequestInfo;
            var client = clientInfo.CurrentClient;
            if (client == null) return;
            client.EndConnect(ar);

            clientInfo.ClientEndpointName = client.LocalEndPoint.ToString();
            clientInfo.ClientPort = ((IPEndPoint)client.LocalEndPoint).Port;
            _listLocalChildrenClients.TryAdd(clientInfo);

            var reponseMessage = HelperClientServerMessage.CreateMessageObject(
                (int)ConstClientServerMessageType.RequestNewChildClient,
                JsonConvert.SerializeObject(new RequestNewChildrenClientDto
                {
                    ServerChildrentEnpoint = clientInfo.ServerEndpointName,
                    ServerChildrentPort = clientInfo.ServerPort
                }));
            HelperClientServerMessage.SendMessageAsync(_remoteClient, reponseMessage);

            Console.WriteLine($"Child Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint}");

            try
            {
                var serverChildClient = _listLocalChildrenClients
                    .Where(e => e.ClientEndpointName == client.LocalEndPoint.ToString())
                    .FirstOrDefault();
                if (serverChildClient == null) return;

                while (true)
                {
                    if (!client.IsConnected()) break;

                    var state = new ClientReadCallbackStateObject(client);

                    var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                    Console.WriteLine($"Has comming message to child client {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                    state.SaveMessageBuffer(read);

                    Console.WriteLine($"Child client {client.LocalEndPoint} revice message: {state.sb}");

                    if (state.revicedBytes.Count > 0)
                    {
                        var messageData = HelperClientServerMessage.CreateMessageObject(
                        (int)ConstClientServerMessageType.Default,
                        serverChildClient.ServerEndpointName,
                        state.revicedBytes);

                        lock (_lock)
                        {
                            HelperClientServerMessage.SendMessageAsync(_remoteClient, messageData);
                        }
                    }
                }
            } catch (Exception ex) { Console.WriteLine(ex); }
        }

    }
}
