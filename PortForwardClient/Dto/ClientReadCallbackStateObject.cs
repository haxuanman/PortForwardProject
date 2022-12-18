using CommonService.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PortForwardClient.Dto
{
    public class ClientReadCallbackStateObject : ReadCallbackStateObject
    {
        public ClientReadCallbackStateObject() { }
        public ClientReadCallbackStateObject(Socket socket) : base(socket) { }
        public async Task SendMessageToRemoteClient(Socket remote)
        {
            await remote.SendAsync(revicedBytes.ToArray(), SocketFlags.None);
        }

    }
}
