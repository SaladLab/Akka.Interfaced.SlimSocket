using System;
using System.Threading.Tasks;
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
        public async Task Run(Communicator communicator)
        {
            // get HelloWorld from Entry

            var entry = communicator.CreateRef<EntryRef>(1);
            var greeter = await entry.GetGreeter();
            if (greeter == null)
                throw new InvalidOperationException("Cannot obtain GreetingActor");

            // add observer

            var observer = communicator.CreateObserver<IGreetObserver>(this);
            await greeter.Subscribe(observer);

            // make some noise

            Console.WriteLine(await greeter.Greet("World"));
            Console.WriteLine(await greeter.Greet("Actor"));
            Console.WriteLine(await greeter.GetCount());

            await greeter.Unsubscribe(observer);
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
            var serializer = PacketSerializer.CreatePacketSerializer();

            var communicator = new Communicator(
                LogManager.GetLogger("Communicator"),
                new IPEndPoint(IPAddress.Loopback, 5000),
                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));

            communicator.StateChanged += (_, state) =>
            {
                if (state == Communicator.StateType.Offline)
                {
                    Console.WriteLine("Disconnected!");
                    Environment.Exit(1);
                }
            };

            communicator.Start();

            var driver = new TestDriver();
            driver.Run(communicator).Wait();
        }
    }
}
