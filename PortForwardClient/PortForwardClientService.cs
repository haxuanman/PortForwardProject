using CommonService.Dto;
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



        public void Stop()
        {
            try
            {
                _localClient.Shutdown(SocketShutdown.Send);
            }
            catch { }
            finally
            {
                _localClient.Close();
            }
        }



        public async Task Send(string data)
        {
            await _localClient.SendAsync(Encoding.UTF8.GetBytes(data), SocketFlags.None);
        }




        public void Start()
        {
            var endPoint = new IPEndPoint(IPAddress.Loopback, 1000);
            _localClient.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), _localClient);
        }



        private void ConnectCallback(IAsyncResult ar)
        {
            var handler = ar.AsyncState as Socket;
            var client = handler;
            client.EndConnect(ar);

            var state = new ReadCallbackStateObject(client);
            client.BeginReceive(state.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), state);
        }



        private void ReadCallback(IAsyncResult ar)
        {
            var handler = ar.AsyncState as ReadCallbackStateObject;
            var client = handler.workSocket;
            if (!handler.workSocket.Connected)
            {
                int read = client.EndReceive(ar);

                if (read > 0)
                {
                    handler.sb.Append(Encoding.UTF8.GetString(handler.buffer, 0, read));
                    client.BeginReceive(handler.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, 0, new AsyncCallback(ReadCallback), handler);
                }
                else
                {
                    Console.WriteLine($"Client Revice message: {handler.sb.ToString()}");
                }
            }
        }
    }
}
