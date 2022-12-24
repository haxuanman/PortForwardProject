using CommonService.ExtensionClass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Dto
{
    public class ItemClientRequestInfo : IDisposable
    {
        public string ClientEndpointName { get; set; } = string.Empty;
        public string ServerEndpointName { get; set; } = string.Empty;

        public int ClientPort { get; set; }
        public int ServerPort { get; set; }

        public Socket CurrentClient { get; set; } = (Socket)null;

        public List<ItemClientRequestInfo> ChildrenClients { get; set; } = new();

        public string MessageQueue { get; set; } = string.Empty;



        public void Dispose()
        {
            CurrentClient.SafeClose();
            foreach(var item in ChildrenClients)
            {
                item.CurrentClient.SafeClose();
            }
        }
    }
}
