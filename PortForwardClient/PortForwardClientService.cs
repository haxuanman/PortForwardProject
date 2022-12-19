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
            if (!client.IsConnected())
            {
                Console.WriteLine($"Client {client.LocalEndPoint} disconnected to server {client.RemoteEndPoint}");
                return;
            }
            client.EndConnect(ar);

            Console.WriteLine($"Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint}");


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

                    Console.WriteLine($"--------Client {client.LocalEndPoint} start receive to server {client.RemoteEndPoint}");
                    var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);
                    Console.WriteLine($"--------Client {client.LocalEndPoint} end receive to server {client.RemoteEndPoint}");

                    Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                    state.SaveMessageBuffer(read);
                    
                    var messageData = HelperClientServerMessage.GetMessageObject(state.revicedBytes);

                    Console.WriteLine($"Client {client.LocalEndPoint} revice message: {messageData.MessageData}");

                    if (messageData.MessageData.Length > 0)
                        await _localClient.SendAsync(Convert.FromBase64String(messageData.MessageData).ToArray(), SocketFlags.None);

                }
            }
            catch { }
        }



        private async void ConnectCallbackLocalClient(IAsyncResult ar)
        {
            var client = ar.AsyncState as Socket;
            if (client == null) return;
            client.EndConnect(ar);

            Console.WriteLine($"Client {client.LocalEndPoint} connected to server {client.RemoteEndPoint}");

            try
            {
                while (true)
                {
                    if (!client.IsConnected()) break;

                    var state = new ClientReadCallbackStateObject(client);

                    Console.WriteLine($"--------Client {client.LocalEndPoint} start receive to server {client.RemoteEndPoint}");
                    var read = await client.ReceiveAsync(state.buffer, SocketFlags.None);
                    Console.WriteLine($"--------Client {client.LocalEndPoint} end receive to server {client.RemoteEndPoint}");

                    Console.WriteLine($"Has comming message to {client.LocalEndPoint} from server {client.RemoteEndPoint}");

                    state.SaveMessageBuffer(read);

                    Console.WriteLine($"Client {client.LocalEndPoint} revice message: {state.sb}");

                    var messageData = HelperClientServerMessage.CreateMessageObject((int)ConstClientServerMessageType.Default, state.revicedBytes);

                    if (messageData.MessageData.Length > 0)
                        await _remoteClient.SendAsync(HelperClientServerMessage.GetMessageBytes(messageData).ToArray(), SocketFlags.None);

                }
            } catch { }
        }

    }
}
