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



        public async void Start()
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


        private async void ReadCallbackLocal(IAsyncResult ar)
        {
            var handler = ar.AsyncState as ReadCallbackStateObject;
            if (handler == null) return;

            var client = handler.workSocket;

            Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

            if (!client.Connected) return;
            if (!client.IsConnected()) return;

            int read = client.EndReceive(ar);

            if (read > 0)
            {
                handler.SaveMessageBuffer(read);

                while (client.Available > 0)
                {
                    read = Math.Min(client.Available, ReadCallbackStateObject.BUFFER_SIZE);

                    client.Receive(handler.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None);

                    handler.SaveMessageBuffer(read);
                }

                Console.WriteLine($"Service {client.LocalEndPoint} revice message from {client.RemoteEndPoint}: {handler.sb}");

                await SendMessageToAllClient(_listenerPublicEndPoint.ToString(), handler.revicedBytes);
            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallbackLocal), resetState);
        }



        private async void ReadCallbackPublic(IAsyncResult ar)
        {
            var handler = ar.AsyncState as ReadCallbackStateObject;
            if (handler == null) return;

            var client = handler.workSocket;

            Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

            if (!client.Connected) return;
            if (!client.IsConnected()) return;

            int read = client.EndReceive(ar);

            if (read > 0)
            {
                handler.SaveMessageBuffer(read);

                while (client.Available > 0)
                {
                    read = Math.Min(client.Available, ReadCallbackStateObject.BUFFER_SIZE);

                    client.Receive(handler.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None);

                    handler.SaveMessageBuffer(read);
                }

                Console.WriteLine($"Service {client.LocalEndPoint} revice message from {client.RemoteEndPoint}: {handler.sb}");

                await SendMessageToAllClient(_listenerLocalEndPoint.ToString(), handler.revicedBytes);
            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallbackPublic), resetState);
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

                    await SendMessageToAllClient(_listenerPublicEndPoint.ToString(), state.revicedBytes);

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

                    await SendMessageToAllClient(_listenerLocalEndPoint.ToString(), state.revicedBytes);

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



        public void ListenCallback(IAsyncResult result)
        {
            
            var listener = result.AsyncState as Socket;
            if (listener == null) return;
            var client = listener.EndAccept(result);

            Console.WriteLine($"New client {client.RemoteEndPoint} connect to server: {listener.LocalEndPoint}");
            _listClients[listener.LocalEndPoint.ToString()].TryAdd(client.RemoteEndPoint.ToString(), client);

            listener.BeginAccept(new AsyncCallback(ListenCallback), listener);

            ReadCallbackStateObject state = new(client);
            client.BeginReceive(state.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), state);

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



        private async void ReadCallback(IAsyncResult ar)
        {
            var handler = ar.AsyncState as ReadCallbackStateObject;
            if (handler == null) return;

            var client = handler.workSocket;
            if (!client.Connected) return;
            if (!client.IsConnected()) return;

            int read = client.EndReceive(ar);

            if (read > 0)
            {
                handler.SaveMessageBuffer(read);

                while (client.Available > 0)
                {
                    read = Math.Min(client.Available, ReadCallbackStateObject.BUFFER_SIZE);

                    client.Receive(handler.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None);

                    handler.SaveMessageBuffer(read);
                }

                Console.WriteLine($"Service {client.LocalEndPoint} revice message from {client.RemoteEndPoint}: {handler.sb}");

                await client.SendAsync(Encoding.UTF8.GetBytes("reviced"), SocketFlags.None);

                await SendMessageToAllClient(_listenerLocalEndPoint.ToString(), handler.revicedBytes);
            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), resetState);
        }
    }
}
