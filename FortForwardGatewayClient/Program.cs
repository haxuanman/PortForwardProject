using FortForwardGatewayClient.Common;
using FortForwardGatewayClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace FortForwardGatewayClient
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello!");

                var hostBuilder = new HostBuilder()
                    .UseNLog()
                    .ConfigureLogging(e => e.SetMinimumLevel(LogLevel.Trace).AddConsole().AddNLogWeb("nlog.config"))
                    .ConfigureAppConfiguration(e =>
                    {
                        e.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices((hostContext, services) =>
                    {

                        var config = hostContext.Configuration.GetSection("HubClientConfig").Get<HubClientConfig>();

                        services.AddSingleton(config);
                        services.AddHostedService<GatewayClientService>();

                    })
                    .UseConsoleLifetime()
                    .Build();

                await hostBuilder.RunAsync();

                Console.WriteLine("Goodbye, Client!");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}