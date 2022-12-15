namespace PortForwardServer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            string command = string.Empty;

            var server = new SocketServer();
            server.Start();

            do
            {
                command = Console.ReadLine();
                if (string.IsNullOrEmpty(command)) break;

                await server.Send(command);

            } while (!string.IsNullOrEmpty(command));

            server.Stop();
        }
    }
}