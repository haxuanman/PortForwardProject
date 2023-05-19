using Microsoft.AspNetCore.WebSockets;
using NLog.Web;

namespace PortForwardServer
{
    public static class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello, Server!");

                var builder = WebApplication.CreateBuilder(args);

                builder.Host.UseNLog();

                builder.WebHost.ConfigureAppConfiguration(webBuilder =>
                {
                    webBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                });

                builder.WebHost.ConfigureServices(services =>
                {
                    services.AddWebSockets(e =>
                    {
                        e.KeepAliveInterval = TimeSpan.FromSeconds(5);
                    });

                    services.AddSignalR();
                });

                var app = builder.Build();

                app.UseRouting();

                app.UseWebSockets();

                app.MapHub<SocketServerHub>("/ServerSocketHub");

                await app.RunAsync();

                Console.WriteLine("Goodbye, Server!");

            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }
    }
}