using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PortForwardServer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello, Server!");

                await new HostBuilder()
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<SocketServer>();
                    })
                    .RunConsoleAsync();
            }
            catch { }
            finally
            {
                Console.ReadKey();
            }
        }
    }
}