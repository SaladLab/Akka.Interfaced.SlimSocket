using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using HelloWorld.Interface;

namespace HelloWorld.Program.Server
{
    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly ActorBoundChannelRef _channel;

        public EntryActor(ActorBoundChannelRef channel)
        {
            _channel = channel;
        }

        [ResponsiveExceptionAll]
        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var actorId = await _channel.BindActor(actor, new TaggedType[] { typeof(IGreeterWithObserver) });
            return (actorId != 0) ? new GreeterWithObserverRef(new BoundActorTarget(actorId)) : null;
        }
    }
}
