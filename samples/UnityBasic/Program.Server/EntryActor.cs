using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
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

        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                new ActorBoundChannelMessage.Bind(actor, typeof(IGreeterWithObserver)));
            return (reply.ActorId != 0) ? new GreeterWithObserverRef(new BoundActorTarget(reply.ActorId)) : null;
        }

        async Task<string> IEntry.GetGreeterOnAnotherChannel()
        {
            if (_environment.Gateway2nd == null)
                throw new InvalidOperationException("Gateway2nd not found!");

            var actor = Context.ActorOf<GreetingActor>();
            var reply = await _environment.Gateway2nd.Ask<ActorBoundGatewayMessage.OpenReply>(
                new ActorBoundGatewayMessage.Open(actor, typeof(IGreeterWithObserver)));
            return reply.Address;
        }

        async Task<ICalculator> IEntry.GetCalculator()
        {
            var actor = Context.ActorOf<CalculatorActor>();

            var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                new ActorBoundChannelMessage.Bind(actor, typeof(ICalculator)));
            return (reply.ActorId != 0) ? new CalculatorRef(new BoundActorTarget(reply.ActorId)) : null;
        }

        async Task<ICounter> IEntry.GetCounter()
        {
            var actor = Context.ActorOf<CounterActor>();
            var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                new ActorBoundChannelMessage.Bind(actor, typeof(ICounter)));
            return (reply.ActorId != 0) ? new CounterRef(new BoundActorTarget(reply.ActorId)) : null;
        }

        async Task<IPedantic> IEntry.GetPedantic()
        {
            var actor = Context.ActorOf<PedanticActor>();
            var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                new ActorBoundChannelMessage.Bind(actor, typeof(IPedantic)));
            return (reply.ActorId != 0) ? new PedanticRef(new BoundActorTarget(reply.ActorId)) : null;
        }
    }
}
