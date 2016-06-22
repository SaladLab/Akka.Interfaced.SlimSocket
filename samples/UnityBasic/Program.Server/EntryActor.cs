using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimServer;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    public class EntryActorEnvironment
    {
        public ActorBoundGatewayRef Gateway;
        public ActorBoundGatewayRef Gateway2nd;
    }

    [ResponsiveExceptionAll]
    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly EntryActorEnvironment _environment;
        private readonly ActorBoundChannelRef _channel;

        public EntryActor(EntryActorEnvironment environment, ActorBoundChannelRef channel)
        {
            _environment = environment;
            _channel = channel.WithRequestWaiter(this);
        }

        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var actorId = await _channel.BindActor(actor, new TaggedType[] { typeof(IGreeterWithObserver) });
            return (actorId != 0) ? new GreeterWithObserverRef(new BoundActorTarget(actorId)) : null;
        }

        async Task<string> IEntry.GetGreeterOnAnotherChannel()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var address = await _environment.Gateway2nd.OpenChannel(actor, new TaggedType[] { typeof(IGreeterWithObserver) });
            return address;
        }

        async Task<ICalculator> IEntry.GetCalculator()
        {
            var actor = Context.ActorOf<CalculatorActor>();
            var actorId = await _channel.BindActor(actor, new TaggedType[] { typeof(ICalculator) });
            return (actorId != 0) ? new CalculatorRef(new BoundActorTarget(actorId)) : null;
        }

        async Task<ICounter> IEntry.GetCounter()
        {
            var actor = Context.ActorOf<CounterActor>();
            var actorId = await _channel.BindActor(actor, new TaggedType[] { typeof(ICounter) });
            return (actorId != 0) ? new CounterRef(new BoundActorTarget(actorId)) : null;
        }

        async Task<IPedantic> IEntry.GetPedantic()
        {
            var actor = Context.ActorOf<PedanticActor>();
            var actorId = await _channel.BindActor(actor, new TaggedType[] { typeof(IPedantic) });
            return (actorId != 0) ? new PedanticRef(new BoundActorTarget(actorId)) : null;
        }
    }
}
