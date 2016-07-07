using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

            using (var system = ActorSystem.Create("MySystem", "akka.loglevel = DEBUG \n akka.actor.debug.lifecycle = on"))
            {
                DeadRequestProcessingActor.Install(system);

                var gateways = new List<GatewayRef>();
                gateways.AddRange(StartGateway(system, ChannelType.Tcp, 5001, 5002));
                gateways.AddRange(StartGateway(system, ChannelType.Udp, 5001, 5002));

                Console.WriteLine("Please enter key to quit.");
                Console.ReadLine();

                Task.WaitAll(gateways.Select(g => g.Stop()).ToArray());
            }
        }

        private static GatewayRef[] StartGateway(ActorSystem system, ChannelType type, int port, int port2)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();
            var environment = new EntryActorEnvironment();

            // First gateway

            var initiator = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                GatewayLogger = LogManager.GetLogger("Gateway"),
                GatewayInitialized = a => { environment.Gateway = a.Cast<ActorBoundGatewayRef>(); },
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep}"),
                ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
                CreateInitialActors = (context, connection) => new[]
                {
                    Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(environment, context.Self.Cast<ActorBoundChannelRef>()))),
                                 new TaggedType[] { typeof(IEntry) },
                                 (ActorBindingFlags)0)
                }
            };

            var gateway = (type == ChannelType.Tcp)
                ? system.ActorOf(Props.Create(() => new TcpGateway(initiator))).Cast<GatewayRef>()
                : system.ActorOf(Props.Create(() => new UdpGateway(initiator))).Cast<GatewayRef>();
            gateway.Start().Wait();

            // Second gateway

            var initiator2 = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port2),
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, port2),
                GatewayLogger = LogManager.GetLogger("Gateway2"),
                TokenRequired = true,
                GatewayInitialized = a => { environment.Gateway2nd = a.Cast<ActorBoundGatewayRef>(); },
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel2({ep}"),
                ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
            };

            var gateway2 = (type == ChannelType.Tcp)
                ? system.ActorOf(Props.Create(() => new TcpGateway(initiator2))).Cast<GatewayRef>()
                : system.ActorOf(Props.Create(() => new UdpGateway(initiator2))).Cast<GatewayRef>();
            gateway2.Start().Wait();

            return new[] { gateway, gateway2 };
        }
    }
}
