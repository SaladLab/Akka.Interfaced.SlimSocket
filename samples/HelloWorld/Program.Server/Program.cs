using System;
using System.Net;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using HelloWorld.Interface;
using Akka.Interfaced.SlimSocket;

namespace HelloWorld.Program.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (typeof(IGreeter) == null)
                throw new Exception("Force interface module to be loaded");

            var system = ActorSystem.Create("MySystem", ""); // "akka.loglevel = DEBUG \n akka.actor.debug.lifecycle = on");
            DeadRequestProcessingActor.Install(system);

            StartGateway(system, ChannelType.Tcp, 5001);
            StartGateway(system, ChannelType.Udp, 5001);

            Console.WriteLine("Please enter key to quit.");
            Console.ReadLine();
        }

        private static void StartGateway(ActorSystem system, ChannelType type, int port)
        {
            var serializer = PacketSerializer.CreatePacketSerializer();

            var initiator = new GatewayInitiator
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, port),
                GatewayLogger = LogManager.GetLogger("Gateway"),
                CreateChannelLogger = (ep, _) => LogManager.GetLogger($"Channel({ep}"),
                ConnectionSettings = new TcpConnectionSettings { PacketSerializer = serializer },
                PacketSerializer = serializer,
                CreateInitialActors = (context, connection) => new[]
                {
                    Tuple.Create(context.ActorOf(Props.Create(() => new EntryActor(context.Self))),
                                 new[] { new ActorBoundChannelMessage.InterfaceType(typeof(IEntry)) })
                }
            };

            var gateway = (type == ChannelType.Tcp)
                ? system.ActorOf(Props.Create(() => new TcpGateway(initiator)))
                : system.ActorOf(Props.Create(() => new UdpGateway(initiator)));

            gateway.Tell(new GatewayMessage.Start());
        }
    }
}
