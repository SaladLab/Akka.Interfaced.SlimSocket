using System;
using System.Net;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using HelloWorld.Interface;
using TypeAlias;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IGreetObserver
    {
        public void Run(Communicator communicator)
        {
            // get HelloWorld from Entry

            var entry = communicator.CreateRef<EntryRef>(1);
            var greeter = entry.GetGreeter().Result;
            if (greeter == null)
                throw new InvalidOperationException("Cannot obtain GreetingActor");

            // add observer

            var observer = communicator.CreateObserver<IGreetObserver>(this);
            greeter.Subscribe(observer);

            // make some noise

            Console.WriteLine(greeter.Greet("World").Result);
            Console.WriteLine(greeter.Greet("Actor").Result);
            Console.WriteLine(greeter.GetCount().Result);

            greeter.Unsubscribe(observer);
            communicator.RemoveObserver(observer);
        }

        void IGreetObserver.Event(string message)
        {
            Console.WriteLine($"<- {message}");
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
