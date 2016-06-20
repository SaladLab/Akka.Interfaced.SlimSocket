using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly IActorRef _channel;

        public EntryActor(IActorRef channel)
        {
            _channel = channel;
        }

        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();
            var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                new ActorBoundChannelMessage.Bind(actor, typeof(IGreeterWithObserver)));
            return (reply.ActorId != 0) ? new GreeterWithObserverRef(new BoundActorTarget(reply.ActorId)) : null;
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
