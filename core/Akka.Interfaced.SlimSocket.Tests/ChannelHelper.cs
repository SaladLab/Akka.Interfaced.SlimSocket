using System;
using System.Net;
using Akka.Actor;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket
{
    public static class ChannelHelper
    {
        private static readonly Server.PacketSerializer s_serverSerializer = Server.PacketSerializer.CreatePacketSerializer();
        private static readonly Client.PacketSerializer s_clientSerializer = Client.PacketSerializer.CreatePacketSerializer();

        public static Server.GatewayRef CreateGateway(ActorSystem system, ChannelType type, string name, IPEndPoint endPoint,
                                                      XunitOutputLogger.Source outputSource,
                                                      Action<Server.GatewayInitiator> clientInitiatorSetup = null)
        {
            // initialize gateway initiator

            var initiator = new Server.GatewayInitiator()
            {
                GatewayLogger = new XunitOutputLogger($"Gateway({name})", outputSource),
                ListenEndPoint = endPoint,
                ConnectEndPoint = endPoint,
                TokenRequired = false,
                CreateChannelLogger = (_, o) => new XunitOutputLogger($"ServerChannel({name})", outputSource),
                CheckCreateChannel = (_, o) => true,
                ConnectionSettings = new Server.TcpConnectionSettings { PacketSerializer = s_serverSerializer },
                PacketSerializer = s_serverSerializer,
            };

            clientInitiatorSetup?.Invoke(initiator);

            // create gateway and start it

            var gateway = (type == ChannelType.Tcp)
                ? system.ActorOf(Props.Create(() => new Server.TcpGateway(initiator))).Cast<Server.GatewayRef>()
                : system.ActorOf(Props.Create(() => new Server.UdpGateway(initiator))).Cast<Server.GatewayRef>();
            gateway.Start().Wait();

            return gateway;
        }

        public static Client.IChannel CreateClientChannel(string name, ChannelType type, IPEndPoint endPoint,
                                                          XunitOutputLogger.Source outputSource)
        {
            return CreateClientChannel(name, $"{type}|{endPoint}|", outputSource);
        }

        public static Client.IChannel CreateClientChannel(string name, string address, XunitOutputLogger.Source outputSource)
        {
            // create channel and start it

            var logger = new XunitOutputLogger($"ClientChannel({name})", outputSource);

            var factory = new Client.ChannelFactory
            {
                CreateChannelLogger = () => logger,
                PacketSerializer = s_clientSerializer
            };

            var udpConfig = ((NetPeerConfiguration)factory.UdpConfig);
            udpConfig.MaximumHandshakeAttempts = 1; // to fail faster

            return factory.Create(address);
        }
    }
}
