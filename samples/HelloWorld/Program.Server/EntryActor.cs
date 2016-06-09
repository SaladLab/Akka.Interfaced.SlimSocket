using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using HelloWorld.Interface;

namespace HelloWorld.Program.Server
{
    public class EntryActor : InterfacedActor, IEntry
    {
        private readonly IActorRef _clientSession;

        public EntryActor(IActorRef clientSession)
        {
            _clientSession = clientSession;
        }

        async Task<IGreeterWithObserver> IEntry.GetGreeter()
        {
            var actor = Context.ActorOf<GreetingActor>();

            try
            {
                var reply = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                    new ActorBoundSessionMessage.Bind(actor, typeof(IGreeterWithObserver)));
                if (reply.ActorId != 0)
                    return BoundActorRef.Create<GreeterWithObserverRef>(reply.ActorId);
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
