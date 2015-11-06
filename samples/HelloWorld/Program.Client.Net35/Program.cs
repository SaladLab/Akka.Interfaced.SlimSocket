using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using HelloWorld.Interface;
using ProtoBuf.Meta;
using TypeAlias;

namespace HelloWorld.Program.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = LogManager.GetLogger("Communicator");

            var serializer = new PacketSerializer(
                new PacketSerializerBase.Data(
                    new ProtoBufMessageSerializer(TypeModel.Create()),
                    new TypeAliasTable()));

            var communicator = new Communicator(logger, new IPEndPoint(IPAddress.Loopback, 5000),
                                                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
            communicator.Start();
            Thread.Sleep(1000);
            var requestWaiter = new SlimTaskRequestWaiter(communicator);
            var helloWorld = new HelloWorldRef(new SlimActorRef(1), requestWaiter, null);

            // Make some noise
            Console.WriteLine(helloWorld.SayHello("World").Result);
            Console.WriteLine(helloWorld.SayHello("Dlrow").Result);
            Console.WriteLine(helloWorld.GetHelloCount().Result);
        }
    }
}
