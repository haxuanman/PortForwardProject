using System.Net;

namespace PortForwardClient
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, Client!");

            string command = string.Empty;

            var client = new PortForwardClientService();
            client.Start();

            do
            {
                command = Console.ReadLine();
                if (string.IsNullOrEmpty(command)) break;

                await client.Send(command);

            } while (!string.IsNullOrEmpty(command));

            client.Stop();
        }
    }
}