namespace PortForwardServer
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Hello, Server!");

                var builder = WebApplication.CreateBuilder(args);

                builder.WebHost.ConfigureAppConfiguration(webBuilder =>
                {
                    webBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                });

                builder.WebHost.ConfigureServices(services =>
                {
                    services.AddSignalR();
                });

                var app = builder.Build();

                app.MapHub<SocketServerHub>("/ServerSocketHub");

                app.Run();

                Console.WriteLine("Goodbye, Server!");

            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }
    }
}