using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CommonService.ExtensionClass
{
    public static class SocketExtensions
    {
        public static void SafeClose(this Socket? socket)
        {
            if (socket == null) return;
            try
            {
                socket.Shutdown(SocketShutdown.Both);

            }
            catch { }
            finally
            {
                socket.Close();
            }
        }



        public static bool IsConnected(this Socket? socket)
        {
            try
            {
                if (socket == null) return false;
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException) { return false; }
        }
    }
}
