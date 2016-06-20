using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using HelloWorld.Interface;

namespace HelloWorld.Program.Server
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

            try
            {
                var reply = await _channel.Ask<ActorBoundChannelMessage.BindReply>(
                    new ActorBoundChannelMessage.Bind(actor, typeof(IGreeterWithObserver)));
                if (reply.ActorId != 0)
                    return new GreeterWithObserverRef(new BoundActorTarget(reply.ActorId));
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
