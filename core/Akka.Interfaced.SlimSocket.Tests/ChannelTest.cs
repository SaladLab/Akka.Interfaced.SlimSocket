using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Interfaced.SlimSocket
{
    public class ChannelTest : TestKit.Xunit2.TestKit
    {
        private static readonly IPEndPoint s_lastTestEndPoint = new IPEndPoint(IPAddress.Loopback, 5060);

        private readonly XunitOutputLogger.Source _outputSource;
        private readonly IPEndPoint _testEndPoint;
        private readonly EntryActorEnvironment _environment;

        public ChannelTest(ITestOutputHelper output)
            : base(output: output)
        {
            _outputSource = new XunitOutputLogger.Source { Output = output, Lock = new object(), Active = true };
            _testEndPoint = s_lastTestEndPoint;
            s_lastTestEndPoint.Port += 2;
            _environment = new EntryActorEnvironment();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_outputSource.Lock)
                    _outputSource.Active = false;
            }

            base.Dispose(disposing);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientConnectToSlimServer(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var reply = await entry.Echo("Test");

            // Assert
            Assert.Equal("Test", reply);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientFailedToConnectToSlimServer(ChannelType type)
        {
            // Act
            var exception = await Record.ExceptionAsync(() => CreatePrimaryClientChannelAsync(type));

            // Assert
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientFailedToRequestWithClosedChannel(ChannelType type)
        {
            // Arrange
            var clientChannel = await CreatePrimaryClientChannelAsync(type, false);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var exception = await Record.ExceptionAsync(() => entry.Echo("Test"));

            // Assert
            Assert.IsType<RequestChannelException>(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientGetExceptionOfPendingRequestAfterChannelClosed(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var exception = await Record.ExceptionAsync(() => entry.Echo("Close"));

            // Assert
            Assert.IsType<RequestChannelException>(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientGetSecondBoundActor(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var greeter = await entry.GetGreeter();
            var reply = await greeter.Greet("World");
            var count = await greeter.GetCount();

            // Assert
            Assert.Equal("Hello World!", reply);
            Assert.Equal(1, count);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientGetsNotificationMessages(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var greeter = await entry.GetGreeter();
            var greetObserver = new TestGreetObserver();
            var observer = clientChannel.CreateObserver<IGreetObserver>(greetObserver);
            await greeter.Subscribe(observer);
            await greeter.Greet("World");
            await greeter.Greet("Actor");
            await greeter.Unsubscribe(observer);
            clientChannel.RemoveObserver(observer);
            await greeter.Greet("Akka");

            // Assert
            Assert.Equal(new[] { "Greet(World)", "Greet(Actor)" }, greetObserver.Logs);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientConnectToSlimServerWithToken(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var gateway2nd = CreateSecondaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();

            // Act
            var greeter = await entry.GetGreeterOnAnotherChannel();
            var reply = await greeter.Greet("World");
            var count = await greeter.GetCount();

            // Assert
            Assert.Equal("Hello World!", reply);
            Assert.Equal(1, count);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task SlimClientConnectToSlimServerWithToken_Timeout(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var gateway2nd = CreateSecondaryGateway(type, i => { i.TokenTimeout = TimeSpan.FromSeconds(0.1); });
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();
            clientChannel.ChannelRouter = null;

            // Act
            var greeter = await entry.GetGreeterOnAnotherChannel();
            await Task.Delay(TimeSpan.FromSeconds(1));
            var greeterTarget = (BoundActorTarget)(((GreeterWithObserverRef)greeter).Target);
            var exception = await Record.ExceptionAsync(() => CreateSecondaryClientChannelAsync(greeterTarget.Address));

            // Assert
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task CloseChannel_ChannelClosed(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();
            Assert.Equal("Test", await entry.Echo("Test"));
            var serverChannel = (await ActorSelection(gateway.CastToIActorRef().Path + "/*").ResolveOne(TimeSpan.FromSeconds(1))).Cast<ActorBoundChannelRef>();

            // Act
            await serverChannel.Close();

            // Assert
            Watch(serverChannel.CastToIActorRef());
            ExpectTerminated(serverChannel.CastToIActorRef());
        }

        [Theory]
        [InlineData(ChannelType.Tcp, 0)]
        [InlineData(ChannelType.Tcp, 2)]
        [InlineData(ChannelType.Udp, 0)]
        [InlineData(ChannelType.Udp, 2)]
        public async Task GatewayStop_AllChannelClosed_ThenStop(ChannelType type, int clientCount)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            for (int i = 0; i < clientCount; i++)
            {
                var clientChannel = await CreatePrimaryClientChannelAsync(type);
                var entry = clientChannel.CreateRef<EntryRef>();
                Assert.Equal("Test:" + i, await entry.Echo("Test:" + i));
            }

            // Act
            await gateway.Stop();

            // Assert
            Watch(gateway.CastToIActorRef());
            ExpectTerminated(gateway.CastToIActorRef());
        }

        [Theory]
        [InlineData(ChannelType.Tcp)]
        [InlineData(ChannelType.Udp)]
        public async Task GatewayStopListen_StopListeningAndKeepActiveConnections(ChannelType type)
        {
            // Arrange
            var gateway = CreatePrimaryGateway(type);
            var clientChannel = await CreatePrimaryClientChannelAsync(type);
            var entry = clientChannel.CreateRef<EntryRef>();
            Assert.Equal("Test", await entry.Echo("Test"));
            var serverChannel = (await ActorSelection(gateway.CastToIActorRef().Path + "/*").ResolveOne(TimeSpan.FromSeconds(1))).Cast<ActorBoundChannelRef>();

            // Act & Assert (Stop listening and further channels cannot be established)
            await gateway.Stop(true);
            var exception = await Record.ExceptionAsync(() => CreatePrimaryClientChannelAsync(type));
            Assert.NotNull(exception);
            Assert.Equal("Test2", await entry.Echo("Test2"));

            // Act & Assert (Stop all and all channels are closed)
            await gateway.Stop();
            Watch(serverChannel.CastToIActorRef());
            ExpectTerminated(serverChannel.CastToIActorRef());
        }

        private Server.GatewayRef CreatePrimaryGateway(ChannelType type, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            return ChannelHelper.CreateGateway(Sys, type, "1", _testEndPoint, _outputSource, initiator =>
            {
                initiator.GatewayInitialized = a => { _environment.Gateway = a.Cast<ActorBoundGatewayRef>(); };
                initiator.CreateInitialActors = (IActorContext context, object socket) => new[]
                {
                    Tuple.Create(
                        context.ActorOf(Props.Create(() => new EntryActor(_environment, context.Self.Cast<ActorBoundChannelRef>()))),
                        new TaggedType[] { typeof(IEntry) },
                        (ActorBindingFlags)0)
                };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private Server.GatewayRef CreateSecondaryGateway(ChannelType type, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            return ChannelHelper.CreateGateway(Sys, type, "2", new IPEndPoint(_testEndPoint.Address, _testEndPoint.Port + 1), _outputSource, initiator =>
            {
                initiator.TokenRequired = true;
                initiator.GatewayInitialized = a => { _environment.Gateway2nd = a.Cast<ActorBoundGatewayRef>(); };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private async Task<Client.IChannel> CreatePrimaryClientChannelAsync(ChannelType type, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel("1", type, _testEndPoint, _outputSource);

            channel.ChannelRouter = (_, address) =>
            {
                try
                {
                    return CreateSecondaryClientChannelAsync(address, true).Result;
                }
                catch (Exception)
                {
                    return null;
                }
            };

            if (connected)
                await channel.ConnectAsync();

            return channel;
        }

        private async Task<Client.IChannel> CreateSecondaryClientChannelAsync(string address, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel("2", address, _outputSource);

            if (connected)
                await channel.ConnectAsync();

            return channel;
        }

        // Close method 확인
        // Stop(listenonly) 확인
    }
}
