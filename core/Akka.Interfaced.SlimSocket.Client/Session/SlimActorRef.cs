using Akka.Interfaced;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class SlimActorRef : IActorRef
    {
        public int Id { get; }

        public SlimActorRef(int id)
        {
            Id = id;
        }
    }
}
