using System;
using System.Threading.Tasks;
using System.Net;
using Akka.Actor;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Interfaced.SlimSocket
{
    public class ChannelTest : IDisposable
    {
        private static readonly IPEndPoint s_lastTestEndPoint = new IPEndPoint(IPAddress.Loopback, 5060);

        private readonly XunitOutputLogger.Source _outputSource;
        private readonly ActorSystem _system;
        private readonly IPEndPoint _testEndPoint;
        private readonly EntryActorEnvironment _environment;

        public ChannelTest(ITestOutputHelper output)
        {
            _outputSource = new XunitOutputLogger.Source { Output = output, Lock = new object(), Active = true };
            _system = ActorSystem.Create("Test");
            _testEndPoint = s_lastTestEndPoint;
            s_lastTestEndPoint.Port += 2;
            _environment = new EntryActorEnvironment();
        }

        public void Dispose()
        {
            _system.Terminate().Wait();

            lock (_outputSource.Lock)
                _outputSource.Active = false;
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
            var token = await entry.GetGreeterOnAnotherChannel();
            var clientChannel2nd = await CreateSecondaryClientChannelAsync(type, token);
            var greeter = clientChannel2nd.CreateRef<GreeterRef>();
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

            // Act
            var token = await entry.GetGreeterOnAnotherChannel();
            await Task.Delay(TimeSpan.FromSeconds(1));
            var exception = await Record.ExceptionAsync(() => CreateSecondaryClientChannelAsync(type, token));

            // Assert
            Assert.NotNull(exception);
        }

        private IActorRef CreatePrimaryGateway(ChannelType type, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            return ChannelHelper.CreateGateway(_system, type, "1", _testEndPoint, _outputSource, initiator =>
            {
                initiator.GatewayInitialized = a => { _environment.Gateway = a; };
                initiator.CreateInitialActors = (IActorContext context, object socket) => new[]
                {
                    Tuple.Create(
                        context.ActorOf(Props.Create(() => new EntryActor(_environment, context.Self))),
                        new[] { new ActorBoundChannelMessage.InterfaceType(typeof(IEntry)) })
                };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private IActorRef CreateSecondaryGateway(ChannelType type, Action<Server.GatewayInitiator> initiatorSetup = null)
        {
            return ChannelHelper.CreateGateway(_system, type, "2", new IPEndPoint(_testEndPoint.Address, _testEndPoint.Port + 1), _outputSource, initiator =>
            {
                initiator.TokenRequired = true;
                initiator.GatewayInitialized = a => { _environment.Gateway2nd = a; };

                initiatorSetup?.Invoke(initiator);
            });
        }

        private async Task<Client.IChannel> CreatePrimaryClientChannelAsync(ChannelType type, bool connected = true)
        {
            var channel = ChannelHelper.CreateClientChannel(type, "1", _testEndPoint, null, _outputSource);
            if (connected)
                await channel.ConnectAsync();
            return channel;
        }

        private async Task<Client.IChannel> CreateSecondaryClientChannelAsync(ChannelType type, string address, bool connected = true)
        {
            var parts = address.Split('|'); // address|port|token
            if (parts.Length < 3)
                throw new ArgumentException(nameof(address));
            var endPoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
            var token = parts[2];
            var channel = ChannelHelper.CreateClientChannel(type, "2", endPoint, token, _outputSource);
            if (connected)
                await channel.ConnectAsync();
            return channel;
        }
    }
}
