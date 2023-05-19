using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace PortForwardClient
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello, Client!");

                var hostBuilder = new HostBuilder()
                    .UseNLog()
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<SocketParentClientService>();
                    })
                    .ConfigureAppConfiguration(e =>
                    {
                        e.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureLogging(e => e.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace).AddConsole().AddNLogWeb("nlog.config"))
                    .UseConsoleLifetime()
                    .Build();

                await hostBuilder.RunAsync();

                Console.WriteLine("Goodbye, Client!");

            } catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}