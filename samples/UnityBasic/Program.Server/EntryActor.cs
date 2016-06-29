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
            var greeter = Context.InterfacedActorOf<GreetingActor>().Cast<GreeterWithObserverRef>();
            return (await _channel.BindActor(greeter)).Cast<GreeterWithObserverRef>();
        }

        async Task<IGreeterWithObserver> IEntry.GetGreeterOnAnotherChannel()
        {
            var greeter = Context.InterfacedActorOf<GreetingActor>().Cast<GreeterWithObserverRef>();
            return (await _environment.Gateway2nd.OpenChannel(greeter)).Cast<GreeterWithObserverRef>();
        }

        async Task<ICalculator> IEntry.GetCalculator()
        {
            var calculator = Context.InterfacedActorOf<CalculatorActor>().Cast<CalculatorRef>();
            return (await _channel.BindActor(calculator)).Cast<CalculatorRef>();
        }

        async Task<ICounter> IEntry.GetCounter()
        {
            CounterRef counter = Context.InterfacedActorOf<CounterActor>().Cast<CounterRef>();
            return (await _channel.BindActor(counter)).Cast<CounterRef>();
        }

        async Task<IPedantic> IEntry.GetPedantic()
        {
            var pedantic = Context.InterfacedActorOf<PedanticActor>().Cast<PedanticRef>();
            return (await _channel.BindActor(pedantic)).Cast<PedanticRef>();
        }
    }
}
