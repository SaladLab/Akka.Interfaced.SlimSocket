using System;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using HelloWorld.Interface;
using ProtoBuf.Meta;
using TypeAlias;

namespace HelloWorld.Program.Server
{
    internal class Program
    {
        private static TcpConnectionSettings s_tcpConnectionSettings;

        private static void Main(string[] args)
        {
            if (typeof(IHelloWorld) == null)
                throw new Exception("Force interface module to be loaded");

            var system = ActorSystem.Create("MySystem");
            DeadRequestProcessingActor.Install(system);

            StartListen(system, 5000);

            Console.WriteLine("Please enter key to quit.");
            Console.ReadLine();
        }

        private static void StartListen(ActorSystem system, int port)
        {
            var logger = LogManager.GetLogger("ClientGateway");

            s_tcpConnectionSettings = new TcpConnectionSettings
            {
                PacketSerializer = new PacketSerializer(
                    new PacketSerializerBase.Data(
                        new ProtoBufMessageSerializer(TypeModel.Create()),
                        new TypeAliasTable()))
            };

            var clientGateway = system.ActorOf(Props.Create(() => new ClientGateway(logger, CreateSession)));
            clientGateway.Tell(new ClientGatewayMessage.Start(new IPEndPoint(IPAddress.Any, port)));
        }

        private static IActorRef CreateSession(IActorContext context, Socket socket)
        {
            var logger = LogManager.GetLogger($"Client({socket.RemoteEndPoint.ToString()})");
            return context.ActorOf(Props.Create(() => new ClientSession(
                logger, socket, s_tcpConnectionSettings, CreateInitialActor)));
        }

        private static Tuple<IActorRef, Type>[] CreateInitialActor(IActorContext context, Socket socket)
        {
            return new[]
            {
                Tuple.Create(context.ActorOf(Props.Create(() => new HelloWorldActor())),
                             typeof(IHelloWorld))
            };
        }
    }
}
