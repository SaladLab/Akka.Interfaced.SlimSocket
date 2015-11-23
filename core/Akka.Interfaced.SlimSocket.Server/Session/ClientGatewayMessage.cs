using System.Net;
using System.Net.Sockets;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class ClientGatewayMessage
    {
        public class Start
        {
            public IPEndPoint ServiceEndPoint { get; }

            public Start(IPEndPoint serviceEndPoint)
            {
                ServiceEndPoint = serviceEndPoint;
            }
        }

        public class Accept
        {
            public Socket Socket { get; }

            public Accept(Socket socket)
            {
                Socket = socket;
            }
        }

        public class Stop
        {
        }
    }
}
