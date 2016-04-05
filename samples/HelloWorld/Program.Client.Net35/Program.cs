using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using HelloWorld.Interface;
using ProtoBuf.Meta;
using TypeAlias;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IHelloWorldEventObserver
    {
        public void Run(Communicator communicator)
        {
            var requestWaiter = new SlimTaskRequestWaiter(communicator);
            var helloWorld = new HelloWorldRef(new SlimActorRef(1), requestWaiter, null);

            // add observer

            var observerId = communicator.IssueObserverId();
            communicator.AddObserver(observerId, new ObserverEventDispatcher(this));
            helloWorld.AddObserver(observerId);

            // make some noise

            Console.WriteLine(helloWorld.SayHello("World").Result);
            Console.WriteLine(helloWorld.SayHello("Dlrow").Result);
            Console.WriteLine(helloWorld.GetHelloCount().Result);
        }

        public void SayHello(string name)
        {
            Console.WriteLine($"<- SayHello({name})");
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var serializer = new PacketSerializer(
                new PacketSerializerBase.Data(
                    new ProtoBufMessageSerializer(TypeModel.Create()),
                    new TypeAliasTable()));

            var communicator = new Communicator(LogManager.GetLogger("Communicator"),
                                                new IPEndPoint(IPAddress.Loopback, 5000),
                                                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
            communicator.Start();

            var driver = new TestDriver();
            driver.Run(communicator);
        }
    }
}
