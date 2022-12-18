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
        private readonly Socket _listenerLocal = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private IPEndPoint _listenerPublicEndPoint;
        private IPEndPoint _listenerLocalEndPoint;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Socket>> _listClients = new ConcurrentDictionary<string, ConcurrentDictionary<string, Socket>>();



        public void Stop()
        {
            try
            {
                _listenerPublic.Shutdown(SocketShutdown.Both);
                _listenerLocal.Shutdown(SocketShutdown.Both);
            }
            catch { }
            finally
            {
                _listenerPublic.Close();
                _listenerLocal.Close();
            }
        }



        public void Start()
        {
            var configuration = HelperConfiguration.GetConfiguration();

            _listenerPublicEndPoint = new IPEndPoint(IPAddress.Any, configuration.GetValue<int>("Server:PublicPort"));
            _listenerLocalEndPoint = new IPEndPoint(IPAddress.Any, configuration.GetValue<int>("Server:LocalPort"));

            _listenerPublic.Bind(_listenerPublicEndPoint);
            _listenerPublic.Listen();
            _listClients.TryAdd(_listenerPublicEndPoint.ToString(), new());

            _listenerLocal.Bind(_listenerLocalEndPoint);
            _listenerLocal.Listen();
            _listClients.TryAdd(_listenerLocalEndPoint.ToString(), new());

            _listenerPublic.BeginAccept(new AsyncCallback(ListenCallbackPublic), _listenerPublic);
            _listenerLocal.BeginAccept(new AsyncCallback(ListenCallbackLocal), _listenerLocal);

        }
        

        
        public async void ListenCallbackLocal(IAsyncResult result)
        {

            var listener = result.AsyncState as Socket;
            if (listener == null) return;
            var client = listener.EndAccept(result);
            if (client.RemoteEndPoint == null) return;

            var clientName = client.RemoteEndPoint.ToString();
            Console.WriteLine($"New client {clientName} connect to server: {listener.LocalEndPoint}");
            _listClients[listener.LocalEndPoint.ToString()].TryAdd(clientName, client);
            Console.WriteLine($"{listener.LocalEndPoint} {_listClients[listener.LocalEndPoint.ToString()].Count()}");

            try
            {
                while (true)
                {
                    if (!client.IsConnected()) break;

                    var state = new ReadCallbackStateObject(client);

                    var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                    Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                    state.SaveMessageBuffer(read);

                    Console.WriteLine($"Client {client.LocalEndPoint} revice message: {state.sb}");

                    var messageData = HelperClientServerMessage.CreateMessageObject((int)ConstClientServerMessageType.Default, state.revicedBytes);

                    await SendMessageToAllClient(_listenerPublicEndPoint.ToString(), HelperClientServerMessage.GetMessageBytes(messageData));

                }
            }
            catch { }
            finally
            {
                _listClients[listener.LocalEndPoint.ToString()].TryRemove(clientName, out _);
            }

            listener.BeginAccept(new AsyncCallback(ListenCallbackLocal), listener);

        }



        public async void ListenCallbackPublic(IAsyncResult result)
        {

            var listener = result.AsyncState as Socket;
            if (listener == null) return;
            var client = listener.EndAccept(result);

            var clientName = client.RemoteEndPoint.ToString();
            Console.WriteLine($"New client {client.RemoteEndPoint} connect to server: {listener.LocalEndPoint}");
            _listClients[listener.LocalEndPoint.ToString()].TryAdd(clientName, client);
            Console.WriteLine($"{listener.LocalEndPoint} {_listClients[listener.LocalEndPoint.ToString()].Count()}");


            try
            {
                while (true)
                {
                    if (!client.IsConnected()) break;
                    var state = new ReadCallbackStateObject(client);
                    var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                    Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                    state.SaveMessageBuffer(read);

                    Console.WriteLine($"Client {client.LocalEndPoint} revice message: {state.sb}");

                    var messageData = HelperClientServerMessage.GetMessageObject(state.revicedBytes);

                    await SendMessageToAllClient(_listenerLocalEndPoint.ToString(), Convert.FromBase64String(messageData.MessageData).ToList());

                }
            }
            catch { }
            finally
            {
                _listClients[listener.LocalEndPoint.ToString()].TryRemove(clientName, out _);
            }

            listener.BeginAccept(new AsyncCallback(ListenCallbackPublic), listener);

        }



        public async Task Send(string data)
        {
            await _listenerPublic.SendAsync(Encoding.UTF8.GetBytes(data), SocketFlags.None);
        }



        private async Task SendMessageToAllClient(string endpoint, List<byte> messages)
        {
            _listClients.TryGetValue(endpoint, out var listClient);

            Console.WriteLine($"SendMessageToAllClient {endpoint} {listClient.Count()}");

            foreach (var client in listClient ?? new())
            {
                if (client.Value == null || !client.Value.IsConnected())
                {
                    _listClients[endpoint].TryRemove(client);
                    continue;
                }

                Console.WriteLine($"SendMessageToAllClient {endpoint} {client.Value.RemoteEndPoint}:");

                Console.WriteLine($"send total {messages.Count} bytes");
                await client.Value.SendAsync(messages.ToArray(), SocketFlags.None);
            }
        }
    }
}
