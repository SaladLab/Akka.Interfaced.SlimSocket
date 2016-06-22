using System;
using System.Net;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (typeof(ICalculator) == null)
                throw new Exception("Force interface module to be loaded");

            var system = ActorSystem.Create("MySystem");
            DeadRequestProcessingActor.Install(system);

            StartGateway(system, ChannelType.Tcp, 5001, 5002);
            StartGateway(system, ChannelType.Udp, 5001, 5002);

            Console.WriteLine("Please enter key to quit.");
            Console.ReadLine();
        }

        private static void StartGateway(ActorSystem system, ChannelType type, int port, int port2)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();
            var environment = new EntryActorEnvironment();

            // First gateway

            var initiator = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                GatewayLogger = LogManager.GetLogger("Gateway"),
                GatewayInitialized = a => { environment.Gateway = new ActorBoundGatewayRef(a); },
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep}"),
                ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
                CreateInitialActors = (context, connection) => new[]
                {
                    Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(environment, new ActorBoundChannelRef(context.Self)))),
                                 new TaggedType[] { typeof(IEntry) },
                                 ChannelClosedNotificationType.Default)
                }
            };

            var gatewayActor = (type == ChannelType.Tcp)
                ? system.ActorOf(Props.Create(() => new TcpGateway(initiator)))
                : system.ActorOf(Props.Create(() => new UdpGateway(initiator)));
            var gateway = new GatewayRef(gatewayActor);
            gateway.Start().Wait();

            // Second gateway

            var initiator2 = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port2),
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port2),
                GatewayLogger = LogManager.GetLogger("Gateway2"),
                TokenRequired = true,
                GatewayInitialized = a => { environment.Gateway2nd = new ActorBoundGatewayRef(a); },
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel2({ep}"),
                ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
            };

            var gatewayActor2 = (type == ChannelType.Tcp)
                ? system.ActorOf(Props.Create(() => new TcpGateway(initiator2)))
                : system.ActorOf(Props.Create(() => new UdpGateway(initiator2)));
            var gateway2 = new GatewayRef(gatewayActor2);
            gateway2.Start().Wait();
        }
    }
}
