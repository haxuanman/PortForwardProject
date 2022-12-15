using CommonService.Dto;
using System;
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

        private readonly Socket _listener = new (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly List<Socket> _clients = new List<Socket>();



        public void Stop()
        {
            try
            {
                _listener.Shutdown(SocketShutdown.Both);
            }
            catch { }
            finally
            {
                _listener.Close();
            }
        }



        public void Start()
        {
            _listener.Bind(new IPEndPoint(IPAddress.Any, 1000));
            _listener.Listen();
            _listener.BeginAccept(new AsyncCallback(ListenCallback), _listener);
        }



        public async Task Send(string data)
        {
            await _listener.SendAsync(Encoding.UTF8.GetBytes(data), SocketFlags.None);
        }



        public void ListenCallback(IAsyncResult result)
        {
            Console.WriteLine("new client Connect");
            var listener = result.AsyncState as Socket;
            var client = listener.EndAccept(result);
            _clients.Add(client);

            listener.BeginAccept(new AsyncCallback(ListenCallback), listener);

            ReadCallbackStateObject state = new(client);
            client.BeginReceive(state.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), state);

        }



        private void ReadCallback(IAsyncResult ar)
        {
            var handler = ar.AsyncState as ReadCallbackStateObject;
            var client = handler.workSocket;
            int read = client.EndReceive(ar);

            if (read > 0)
            {
                handler.sb.Append(Encoding.UTF8.GetString(handler.buffer, 0, read));

                while (client.Available > 0)
                {
                    client.Receive(handler.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None);

                    handler.sb.Append(Encoding.UTF8.GetString(handler.buffer, 0, read));
                }

                Console.WriteLine($"Service Revice message: {handler.sb.ToString()}");
            }

            var resetState = new ReadCallbackStateObject(client);
            client.BeginReceive(resetState.buffer, 0, ReadCallbackStateObject.BUFFER_SIZE, SocketFlags.None, new AsyncCallback(ReadCallback), resetState);
        }
    }
}
