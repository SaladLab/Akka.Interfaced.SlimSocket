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

        async Task<IHelloWorld> IEntry.GetHelloWorld()
        {
            var actor = Context.ActorOf<HelloWorldActor>();

            try
            {
                var reply = await _clientSession.Ask<ActorBoundSessionMessage.BindReply>(
                    new ActorBoundSessionMessage.Bind(actor, typeof(IHelloWorld)));
                if (reply.ActorId != 0)
                    return BoundActorRef.Create<HelloWorldRef>(reply.ActorId);
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
