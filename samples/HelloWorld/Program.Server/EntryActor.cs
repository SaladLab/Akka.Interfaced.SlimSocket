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
            var greeter = Context.InterfacedActorOf<GreetingActor>().Cast<GreeterWithObserverRef>();
            return (await _channel.BindActor(greeter)).Cast<GreeterWithObserverRef>();
        }
    }
}
