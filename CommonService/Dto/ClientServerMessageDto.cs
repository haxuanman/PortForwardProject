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
        public string MessageData { get; set; } = string.Empty;
    }


    public enum ConstClientServerMessageType
    {
        Default = 0,
    }
}
