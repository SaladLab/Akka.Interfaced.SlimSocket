using System.Net;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using ProtoBuf.Meta;
using TypeAlias;

namespace UniversalChat.Program.Client.Console
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var serializer = new PacketSerializer(
                new PacketSerializerBase.Data(
                    new ProtoBufMessageSerializer(TypeModel.Create()),
                    new TypeAliasTable()));

            var communicator = new Communicator(LogManager.GetLogger("Communicator"),
                                                new IPEndPoint(IPAddress.Loopback, 9001),
                                                _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
            communicator.Start();

            var driver = new ChatConsole();
            driver.RunAsync(communicator).Wait();
        }
    }
}
