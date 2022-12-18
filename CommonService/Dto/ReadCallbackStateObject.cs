using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.Dto
{
    public class ReadCallbackStateObject
    {

        public ReadCallbackStateObject() { }

        public ReadCallbackStateObject(Socket socket) { workSocket = socket; }

        public Socket workSocket;
        public const int BUFFER_SIZE = 65536;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public StringBuilder sb = new StringBuilder();
        public List<byte> revicedBytes = new List<byte>();

        public void SaveMessageBuffer(int read)
        {
            revicedBytes.AddRange(buffer.Take(read));
            sb.Append(Convert.ToBase64String(buffer.Take(read).ToArray()));
            buffer = new byte[BUFFER_SIZE];
        }
    }
}
