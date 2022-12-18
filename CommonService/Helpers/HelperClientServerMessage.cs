using CommonService.Dto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Helpers
{
    public class HelperClientServerMessage
    {

        public static ClientServerMessageDto CreateMessageObject(int type, List<byte> data)
        {
            return new ClientServerMessageDto
            {
                MessageType = type,
                MessageData = Convert.ToBase64String(data.ToArray())
            };
        }

        public static ClientServerMessageDto CreateMessageObject(int type, string data)
        {
            return new ClientServerMessageDto
            {
                MessageType = type,
                MessageData = JsonConvert.SerializeObject(data)
            };
        }

        public static List<byte> GetMessageBytes(ClientServerMessageDto message)
        {
            var messageStr = JsonConvert.SerializeObject(message);
            return Encoding.UTF8.GetBytes(messageStr).ToList();
        }

        public static ClientServerMessageDto GetMessageObject(List<byte> messageBytes)
        {
            var messageStr = Encoding.UTF8.GetString(messageBytes.ToArray());
            return JsonConvert.DeserializeObject<ClientServerMessageDto>(messageStr) ?? new();
        }
    }
}
