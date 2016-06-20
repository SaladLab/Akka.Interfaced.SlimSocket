using System.Net;
using Lidgren.Network;
using Akka.Interfaced.SlimSocket.Base;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class ClientUdpGatewayMessage
    {
        public class Start
        {
            public IPEndPoint ServiceEndPoint { get; }

            public Start(IPEndPoint serviceEndPoint)
            {
                ServiceEndPoint = serviceEndPoint;
            }
        }

        internal class Accept
        {
            public NetConnection Connection { get; }

            public Accept(NetConnection connection)
            {
                Connection = connection;
            }
        }

        public class Stop
        {
        }
    }

    // temporary class for prototyping
    public class ClientUdpSessionMessage
    {
        public class Close
        {
        }

        public class Receive
        {
            public Packet Packet;
        }
    }
}
