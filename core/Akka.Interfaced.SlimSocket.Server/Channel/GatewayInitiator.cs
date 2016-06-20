using System;
using System.Net;
using Akka.Actor;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class GatewayInitiator
    {
        public IPEndPoint ListenEndPoint { get; set; }
        public IPEndPoint ConnectEndPoint { get; set; }
        public ILog GatewayLogger { get; set; }
        public bool TokenRequired { get; set; }
        public TimeSpan TokenTimeout { get; set; }
        public Action<IActorRef> GatewayInitialized { get; set; }
        public Func<EndPoint, object, ILog> CreateChannelLogger { get; set; }
        public Func<EndPoint, object, bool> CheckCreateChannel { get; set; }
        public TcpConnectionSettings ConnectionSettings { get; set; }
        public IPacketSerializer PacketSerializer { get; set; }
        public object UdpConfig { get; set; }
        public Func<IActorContext, object, Tuple<IActorRef, ActorBoundChannelMessage.InterfaceType[]>[]> CreateInitialActors { get; set; }

        public GatewayInitiator()
        {
            TokenTimeout = TimeSpan.FromSeconds(30);

            var udpConfig = new NetPeerConfiguration("SlimSocket");
            udpConfig.AutoExpandMTU = true;
            UdpConfig = udpConfig;
        }
    }
}
