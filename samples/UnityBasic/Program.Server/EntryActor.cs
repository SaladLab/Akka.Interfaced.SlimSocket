using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly IActorRef _clientSession;

        public EntryActor(IActorRef clientSession)
        {
            _clientSession = clientSession;
        }

        async Task<IHelloWorld> IEntry.GetHelloWorld()
        {
            var actor = Context.ActorOf<HelloWorldActor>();

            var reply = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                new ActorBoundSessionMessage.Bind(actor, typeof(IHelloWorld), null));

            return BoundActorRef.Create<HelloWorldRef>(reply.ActorId);
        }

        async Task<ICalculator> IEntry.GetCalculator()
        {
            var actor = Context.ActorOf<CalculatorActor>();

            var reply = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                new ActorBoundSessionMessage.Bind(actor, typeof(ICalculator), null));

            return BoundActorRef.Create<CalculatorRef>(reply.ActorId);
        }

        async Task<ICounter> IEntry.GetCounter()
        {
            var actor = Context.ActorOf<CounterActor>();

            var reply = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                new ActorBoundSessionMessage.Bind(actor, typeof(ICounter), null));

            return BoundActorRef.Create<CounterRef>(reply.ActorId);
        }

        async Task<IPedantic> IEntry.GetPedantic()
        {
            var actor = Context.ActorOf<PedanticActor>();

            var reply = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                new ActorBoundSessionMessage.Bind(actor, typeof(IPedantic), null));

            return BoundActorRef.Create<PedanticRef>(reply.ActorId);
        }
    }
}
