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
        public const int BUFFER_SIZE = 4;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public StringBuilder sb = new StringBuilder();
    }
}
