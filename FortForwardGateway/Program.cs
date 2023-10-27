using FortForwardGateway.Hubs;
using Microsoft.AspNetCore.WebSockets;
using NLog.Web;

namespace FortForwardGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseNLog()
                .ConfigureLogging(e => e.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace).AddConsole().AddNLogWeb("nlog.config"));

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddWebSockets(e =>
            {
            });

            builder.Services.AddSignalR(e =>
            {
                e.EnableDetailedErrors = true;
                e.MaximumReceiveMessageSize = 65536; // byte
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            //app.UseAuthorization();

            app.UseWebSockets();

            app.MapHub<PortForwardServerHub>("/PortForwardServerHub");

            app.MapControllers();

            app.Run();
        }
    }
}