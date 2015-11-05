using System;
using Akka.Actor;

namespace Akka.Interfaced.SlimSocket.Server
{
    public static class ClientSessionMessage
    {
        public class BindActorRequest
        {
            public IActorRef Actor;
            public Type InterfaceType;
            public object TagValue;
        }

        public class BindActorResponse
        {
            public int ActorId;
        }

        public class UnbindActorRequest
        {
            public IActorRef Actor;
            public int ActorId;
        }

        public class BoundSessionTerminated
        {
        }
    }
}
