using CommonService.Dto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Helpers
{
    public static class HelperClientServerMessage
    {

        public static int SendMessageAsync(Socket client, ClientServerMessageDto input)
        {
            return client.Send(GetMessageBytes(input).ToArray(), SocketFlags.None);
        }



        public static ClientServerMessageDto CreateMessageObject(int type, string serverChildEnpoint, List<byte> data)
        {
            return new ClientServerMessageDto
            {
                MessageType = type,
                ServerChildEndPoint = serverChildEnpoint,
                MessageData = Convert.ToBase64String(data.ToArray())
            };
        }

        public static ClientServerMessageDto CreateMessageObject(int type, string data)
        {
            return new ClientServerMessageDto
            {
                MessageType = type,
                MessageData = data
            };
        }

        public static List<byte> GetMessageBytes(ClientServerMessageDto message)
        {
            var messageStr = $"{JsonConvert.SerializeObject(message)}|";
            return Encoding.UTF8.GetBytes(messageStr).ToList();
        }

        public static ClientServerMessageDto GetMessageObject(List<byte> messageBytes)
        {
            var messageStr = Encoding.UTF8.GetString(messageBytes.ToArray());
            if (messageStr.EndsWith("|")) messageStr = messageStr.Remove(messageStr.Length - 1);
            return JsonConvert.DeserializeObject<ClientServerMessageDto>(messageStr) ?? new();
        }



        public static List<ClientServerMessageDto> GetListMessageObject(ref string messageQueue)
        {
            if (string.IsNullOrEmpty(messageQueue)) return new List<ClientServerMessageDto>();

            var listMessage = messageQueue.Split("|")
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();

            if (listMessage.Count == 0) return new List<ClientServerMessageDto>();

            if (!messageQueue.EndsWith("|"))
            {
                messageQueue = listMessage.LastOrDefault("");
                listMessage.RemoveAt(listMessage.Count - 1);
                if (listMessage.Count == 0) return new List<ClientServerMessageDto>();
            } else
            {
                messageQueue = string.Empty;
            }
            
            var listResult = listMessage.Select(e => JsonConvert.DeserializeObject<ClientServerMessageDto>(e)).ToList();

            return listResult!;
        }
    }
}
