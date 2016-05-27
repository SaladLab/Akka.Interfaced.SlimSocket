using System;
using System.Net;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using HelloWorld.Interface;
using TypeAlias;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IHelloWorldEventObserver
    {
        public void Run(Communicator communicator)
        {
            // get HelloWorld from Entry

            var entry = communicator.CreateRef<EntryRef>(1);
            var helloWorld = entry.GetHelloWorld().Result;
            if (helloWorld == null)
                throw new InvalidOperationException("Cannot retreive HelloWorld actor");

            // add observer

            var observer = communicator.CreateObserver<IHelloWorldEventObserver>(this);
            helloWorld.AddObserver(observer);

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
                    new ProtoBufMessageSerializer(PacketSerializer.CreateTypeModel()),
                    new TypeAliasTable()));

            var communicator = new Communicator(LogManager.GetLogger("Communicator"),
                                                new IPEndPoint(IPAddress.Loopback, 5000),
                                                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
            communicator.TaskFactory = new SlimTaskFactory();
            communicator.Start();

            var driver = new TestDriver();
            driver.Run(communicator);
        }
    }
}
