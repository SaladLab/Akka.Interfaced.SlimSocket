using System;
using System.Net;
using Akka.Interfaced.SlimSocket;
using Akka.Interfaced.SlimSocket.Client;
using HelloWorld.Interface;

namespace HelloWorld.Program.Client
{
    internal class TestDriver : IGreetObserver
    {
        public void Run(IChannel channel)
        {
            var connected = channel.ConnectAsync().Result;

            // get HelloWorld from Entry

            var entry = channel.CreateRef<EntryRef>(1);
            var greeter = entry.GetGreeter().Result;
            if (greeter == null)
                throw new InvalidOperationException("Cannot obtain GreetingActor");

            // add observer

            var observer = channel.CreateObserver<IGreetObserver>(this);
            greeter.Subscribe(observer);

            // make some noise

            Console.WriteLine(greeter.Greet("World").Result);
            Console.WriteLine(greeter.Greet("Actor").Result);
            Console.WriteLine(greeter.GetCount().Result);

            greeter.Unsubscribe(observer);
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
            driver.Run(channelFactory.Create());

            // UDP
            channelFactory.Type = ChannelType.Udp;
            driver.Run(channelFactory.Create());
        }
    }
}
