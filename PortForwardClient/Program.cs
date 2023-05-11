using Microsoft.Extensions.Configuration;
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

                var hostBuilder = new HostBuilder()
                        .ConfigureServices((hostContext, services) =>
                        {
                            services.AddHostedService<SocketParentClientService>();
                        })
                        .ConfigureAppConfiguration(e =>
                        {
                            e.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                        })
                        .Build();

                await hostBuilder.RunAsync();

                Console.WriteLine("Goodbye, Client!");

            } catch (Exception ex) { Console.WriteLine(ex); }
        }
    }
}