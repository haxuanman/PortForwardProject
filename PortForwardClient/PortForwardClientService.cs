using CommonService.Dto;
using CommonService.ExtensionClass;
using CommonService.Helpers;
using Microsoft.Extensions.Configuration;
using PortForwardClient.Dto;
using System;
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
            
            _localClient.BeginConnect(localEndPoint, new AsyncCallback(ConnectCallbackLocalClient), _localClient);
            _remoteClient.BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallbackRemoteClient), _remoteClient);
        }



        private async void ConnectCallbackRemoteClient(IAsyncResult ar)
        {
            var client = ar.AsyncState as Socket;
            if (client == null) return;
            client.EndConnect(ar);

            Console.WriteLine($"Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint}");


            while (true)
            {
                var state = new ClientReadCallbackStateObject(client);
                var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                state.SaveMessageBuffer(read);

                Console.WriteLine($"Client {client.LocalEndPoint} revice message: {state.sb}");

                await state.SendMessageToRemoteClient(_localClient);

            }
        }


        private async void ReadCallbackRemoteClient(IAsyncResult ar)
        {
            var handler = ar.AsyncState as ClientReadCallbackStateObject;
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

                Console.WriteLine($"Client {client.LocalEndPoint} revice message: {handler.sb}");

                await handler.SendMessageToRemoteClient(_localClient);
            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallbackRemoteClient), resetState);
        }



        private async void ConnectCallbackLocalClient(IAsyncResult ar)
        {
            var client = ar.AsyncState as Socket;
            if (client == null) return;
            client.EndConnect(ar);

            Console.WriteLine($"Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint}");

            while (true)
            {
                var state = new ClientReadCallbackStateObject(client);
                var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);

                Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                state.SaveMessageBuffer(read);

                Console.WriteLine($"Client {client.LocalEndPoint} revice message: {state.sb}");

                await state.SendMessageToRemoteClient(_remoteClient);

            }
        }


        private async void ReadCallbackLocalClient(IAsyncResult ar)
        {

            var handler = ar.AsyncState as ClientReadCallbackStateObject;
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

                Console.WriteLine($"Client {client.LocalEndPoint} revice message: {handler.sb}");

                await handler.SendMessageToRemoteClient(_remoteClient);
            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallbackLocalClient), resetState);
        }



        private void ConnectCallback(IAsyncResult ar)
        {
            var client = ar.AsyncState as Socket;
            if (client == null) return;
            client.EndConnect(ar);

            var state = new ReadCallbackStateObject(client);
            client.BeginReceive(state.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), state);
        }



        private void ReadCallback(IAsyncResult ar)
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

                Console.WriteLine($"Client {client.LocalEndPoint} revice message: {handler.sb}");

            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), resetState);
        }
    }
}
