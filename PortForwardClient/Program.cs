using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PortForwardClient
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello, Client!");

                await new HostBuilder()
                        .ConfigureServices((hostContext, services) =>
                        {
                            services.AddHostedService<PortForwardClientService>();
                        })
                        .RunConsoleAsync();

                Console.WriteLine("Goodbye, Client!");
            } catch { }
            finally { }
        }
    }
}