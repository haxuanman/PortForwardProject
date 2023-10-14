using Microsoft.AspNetCore.SignalR.Client;

namespace FortForwardGatewayClient.Common
{
    internal class SignalrAlwaysRetryPolicy : IRetryPolicy
    {

        private readonly TimeSpan _delayTime = TimeSpan.FromSeconds(10);

        public SignalrAlwaysRetryPolicy() { }

        public SignalrAlwaysRetryPolicy(TimeSpan delayTime)
        {
            _delayTime = delayTime;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            Console.WriteLine($"Retry Connect {_delayTime.TotalSeconds}s");
            return _delayTime;
        }

    }
}
