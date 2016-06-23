using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using HelloWorld.Interface;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IGreetObserver
    {
        public async Task Run(IChannel channel)
        {
            await channel.ConnectAsync();

            // get HelloWorld from Entry

            var entry = channel.CreateRef<EntryRef>(1);
            var greeter = await entry.GetGreeter();
            if (greeter == null)
                throw new InvalidOperationException("Cannot obtain GreetingActor");

            // add observer

            var observer = channel.CreateObserver<IGreetObserver>(this);
            await greeter.Subscribe(observer);

            // make some noise

            Console.WriteLine(await greeter.Greet("World"));
            Console.WriteLine(await greeter.Greet("Actor"));
            Console.WriteLine(await greeter.GetCount());

            await greeter.Unsubscribe(observer);
            channel.RemoveObserver(observer);
            channel.Close();
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
            var channelFactory = new ChannelFactory
            {
                Type = ChannelType.Tcp,
                ConnectEndPoint = new IPEndPoint(IPAddress.Loopback, 5001),
                CreateChannelLogger = () => null,
                PacketSerializer = PacketSerializer.CreatePacketSerializer()
            };

            // TCP
            var driver = new TestDriver();
            driver.Run(channelFactory.Create()).Wait();

            // UDP
            channelFactory.Type = ChannelType.Udp;
            driver.Run(channelFactory.Create()).Wait();
        }
    }
}
