using Microsoft.AspNetCore.SignalR.Client;

namespace PortForwardClient.Common
{
    internal class SignalrAlwaysRetryPolicy : IRetryPolicy
    {

        private readonly TimeSpan _delayTime;

        public SignalrAlwaysRetryPolicy() { }

        public SignalrAlwaysRetryPolicy(TimeSpan delayTime)
        {
            _delayTime = delayTime;
        }

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            Console.WriteLine("Retry Connect");
            return _delayTime;
        }

    }
}
