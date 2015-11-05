using System.Net;
using System.Net.Sockets;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class ClientGatewayMessage
    {
        public class Start
        {
            public IPEndPoint ServiceEndPoint;
        }

        public class Accept
        {
            public Socket Socket;
        }

        public class Shutdown
        {
        }
    }
}
