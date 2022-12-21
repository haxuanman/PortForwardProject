using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Dto
{
    public class ClientServerMessageDto
    {
        public int MessageType { get; set; }

        public string ServerChildEndPoint { get; set; } = string.Empty;

        public string MessageData { get; set; } = string.Empty;
    }


    public enum ConstClientServerMessageType : int
    {
        Default = 0,
        RequestNewClient = 1,
        RequestNewChildClient = 2,
    }
}
