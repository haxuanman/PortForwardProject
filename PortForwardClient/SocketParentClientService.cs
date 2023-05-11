using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PortForwardClient
{
    public class SocketParentClientService : IHostedService
    {

        private readonly HubConnection _connection;


        public SocketParentClientService(IConfiguration configuration)
        {

            _connection = new HubConnectionBuilder()
                .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(5) } )
                .WithUrl($"{configuration["ServerUrl"]}/ServerSocketHub")
                .Build();

        }



        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _connection.StartAsync();
        }



        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _connection.StopAsync();
            } catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
