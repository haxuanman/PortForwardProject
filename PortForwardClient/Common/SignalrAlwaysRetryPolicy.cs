using Microsoft.AspNetCore.SignalR.Client;

namespace PortForwardClient.Common
{
    internal class SignalrAlwaysRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            Console.WriteLine("Retry Connect");
            return TimeSpan.FromSeconds(10);
        }

    }
}
