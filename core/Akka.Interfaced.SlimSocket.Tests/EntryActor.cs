using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced.SlimSocket.Server;

namespace Akka.Interfaced.SlimSocket
{
    public class EntryActorEnvironment
    {
        public IActorRef Gateway;
        public IActorRef Gateway2nd;
    }

    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly EntryActorEnvironment _environment;
        private readonly IActorRef _channel;

        public EntryActor(EntryActorEnvironment environment, IActorRef channel)
        {
            _environment = environment;
            _channel = channel;
        }

        Task<string> IEntry.Echo(string message)
        {
            if (message == "Close")
                _channel.Tell(PoisonPill.Instance);

            return Task.FromResult(message);
        }

        [ResponsiveExceptionAll]
        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                new ActorBoundChannelMessage.Bind(actor, typeof(IGreeterWithObserver)));
            return (reply.ActorId != 0)
                ? new GreeterWithObserverRef(new BoundActorTarget(reply.ActorId))
                : null;
        }

        [ResponsiveExceptionAll]
        async Task<string> IEntry.GetGreeterOnAnotherChannel()
        {
            if (_environment.Gateway2nd == null)
                throw new InvalidOperationException("Gateway2nd not found!");

            var actor = Context.ActorOf<GreetingActor>();
            var reply = await _environment.Gateway2nd.Ask<ActorBoundGatewayMessage.OpenReply>(
                new ActorBoundGatewayMessage.Open(actor, typeof(IGreeterWithObserver)));
            return reply.Address;
        }
    }
}
