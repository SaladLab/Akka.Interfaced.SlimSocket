using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced.SlimServer;

namespace Akka.Interfaced.SlimSocket
{
    public class EntryActorEnvironment
    {
        public ActorBoundGatewayRef Gateway;
        public ActorBoundGatewayRef Gateway2nd;
    }

    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly EntryActorEnvironment _environment;
        private readonly ActorBoundChannelRef _channel;

        public EntryActor(EntryActorEnvironment environment, ActorBoundChannelRef channel)
        {
            _environment = environment;
            _channel = channel.WithRequestWaiter(this);
        }

        Task<string> IEntry.Echo(string message)
        {
            // TODO: Now PoisonPill but close command?
            if (message == "Close")
                _channel.Actor.Tell(PoisonPill.Instance);

            return Task.FromResult(message);
        }

        [ResponsiveExceptionAll]
        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var actorId = await _channel.BindActor(actor, new TaggedType[] { typeof(IGreeterWithObserver) });
            return (actorId != 0) ? new GreeterWithObserverRef(new BoundActorTarget(actorId)) : null;
        }

        [ResponsiveExceptionAll]
        async Task<string> IEntry.GetGreeterOnAnotherChannel()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var address = await _environment.Gateway2nd.OpenChannel(actor, new TaggedType[] { typeof(IGreeterWithObserver) });
            return address;
        }
    }
}
