namespace PortForwardServer.Dal
{
    public class HandleIncomingConnectionStateDto
    {

        public ISocketServerHub? Caller { get; set; }

        public string? ConnectionId { get; set; }

    }
}
