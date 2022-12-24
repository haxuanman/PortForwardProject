using CommonService.Dto;
using CommonService.ExtensionClass;
using CommonService.Helpers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PortForwardServer
{
    public class SocketServer
    {

        private readonly Socket _listenerPublic = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly BlockingCollection<ItemClientRequestInfo> _listPublicClients = new();
        private readonly IConfiguration _configuration = HelperConfiguration.GetConfiguration();
        private readonly object _lock = new object();


        public void Stop()
        {
            try
            {
                _listenerPublic.Shutdown(SocketShutdown.Both);
            }
            catch { }
            finally
            {
                _listenerPublic.Close();
            }
        }



        public void Start()
        {

            var listenerPublicEndPoint = new IPEndPoint(IPAddress.Any, _configuration.GetValue<int>("Server:PublicPort"));
            _listenerPublic.Bind(listenerPublicEndPoint);
            _listenerPublic.Listen();
            _listenerPublic.BeginAccept(new AsyncCallback(ListenCallbackPublic), _listenerPublic);

        }


        
        public async void ListenCallbackLocal(IAsyncResult result)
        {

            var listener = result.AsyncState as Socket;
            if (listener == null) return;
            var client = listener.EndAccept(result);
            if (client.RemoteEndPoint == null) return;

            var clientName = client.RemoteEndPoint.ToString();
            Console.WriteLine($"New Child client {clientName} connect to server {listener.LocalEndPoint}");

            #region tạo child client từ client

            var requestChildClient = new RequestNewChildrenClientDto
            {
                ServerChildrentEnpoint = client.RemoteEndPoint?.ToString(),
                ServerChildrentPort = ((IPEndPoint)client.RemoteEndPoint).Port,

                ServerPort = ((IPEndPoint)client.LocalEndPoint).Port,
            };

            var messageNewChildClient = HelperClientServerMessage.CreateMessageObject(
                (int)ConstClientServerMessageType.RequestNewChildClient,
                JsonConvert.SerializeObject(requestChildClient));


            var remoteClient = _listPublicClients.Where(e => e.ServerPort == ((IPEndPoint)client.LocalEndPoint).Port).FirstOrDefault();
            if (remoteClient == null) return;
            await remoteClient.CurrentClient.SendAsync(HelperClientServerMessage.GetMessageBytes(messageNewChildClient).ToArray(), SocketFlags.None);


            remoteClient.ChildrenClients.Add(new ItemClientRequestInfo
            {
                CurrentClient = client,

                ServerPort = requestChildClient.ServerPort,
                ServerEndpointName = requestChildClient.ServerChildrentEnpoint,
            });

            byte[] bufferTmp = new byte[remoteClient.CurrentClient.ReceiveBufferSize];
            await remoteClient.CurrentClient.ReceiveAsync(bufferTmp, SocketFlags.None);

            #endregion

            listener.BeginAccept(new AsyncCallback(ListenCallbackLocal), listener);

            try
            {

                while (true)
                {
                    if (!client.IsConnected())
                    {
                        break;
                    }

                    var state = new ReadCallbackStateObject(client);

                    var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                    Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint} {read} bytes");

                    state.SaveMessageBuffer(read);

                    Console.WriteLine($"Child Client {client.LocalEndPoint} revice message: {state.sb}");

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
        }



        public async void ListenCallbackPublic(IAsyncResult result)
        {

            var listener = result.AsyncState as Socket;
            if (listener == null) return;
            var client = listener.EndAccept(result);

            var clientName = client.RemoteEndPoint.ToString();
            Console.WriteLine($"Client {client.RemoteEndPoint} connected to server {listener.LocalEndPoint}");

            #region tạo port cho client
            var requestNewPortMgsBytes = new byte[client.ReceiveBufferSize];
            int reviceByte = await client.ReceiveAsync(requestNewPortMgsBytes, SocketFlags.None);
            var requestNewPortMgs = HelperClientServerMessage.GetMessageObject(requestNewPortMgsBytes.ToList().Take(reviceByte).ToList());
            var requestNewPortInfo = JsonConvert.DeserializeObject<ClientRequestNewPortDto>(requestNewPortMgs.MessageData);
            
            var listenerLocalEndPoint = new IPEndPoint(IPAddress.Any, requestNewPortInfo.RequestPort);
            Socket listenerLocal = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenerLocal.Bind(listenerLocalEndPoint);
            listenerLocal.Listen();
            Console.WriteLine($"Created listen port {((IPEndPoint)listenerLocal.LocalEndPoint).Port} for client {client.RemoteEndPoint}");

            var clientInfo = new ItemClientRequestInfo
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

            listener.BeginAccept(new AsyncCallback(ListenCallbackPublic), listener);

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

                    Console.WriteLine($"Has comming message from server {client.RemoteEndPoint} to client {client.LocalEndPoint}: {read} bytes");

                    state.SaveMessageBuffer(read);

                    var messageData = HelperClientServerMessage.GetMessageObject(state.revicedBytes);

                    Console.WriteLine($"Service {client.LocalEndPoint} revice message: {messageData.MessageData}");

                    switch (messageData.MessageType)
                    {
                        case (int)ConstClientServerMessageType.Default:
                            Console.WriteLine(messageData.MessageData);

                            var clientRemoteInfo = _listPublicClients
                                .Where(e => e.ClientEndpointName == client.RemoteEndPoint.ToString())
                                .FirstOrDefault();

                            var sendToChildClient = clientRemoteInfo.ChildrenClients.Where(e => e.ServerEndpointName == messageData.ServerChildEndPoint)
                                .FirstOrDefault();

                            lock (_lock)
                            {
                                sendToChildClient.CurrentClient.Send(Convert.FromBase64String(messageData.MessageData), SocketFlags.None);
                            }

                            break;
                        case (int)ConstClientServerMessageType.RequestNewClient:
                            break;
                        case (int)ConstClientServerMessageType.RequestNewChildClient:

                            var messageNewChildClient = JsonConvert.DeserializeObject<RequestNewChildrenClientDto>(messageData.MessageData);

                            clientRemoteInfo = _listPublicClients
                                .Where(e => e.ClientEndpointName == client.RemoteEndPoint.ToString())
                                .FirstOrDefault();

                            await clientRemoteInfo.CurrentClient.SendAsync(state.revicedBytes.ToArray(), SocketFlags.None);

                            break;

                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            finally { }
            #endregion
        }



        public async Task Send(string data)
        {
            await _listenerPublic.SendAsync(Encoding.UTF8.GetBytes(data), SocketFlags.None);
        }

    }
}
