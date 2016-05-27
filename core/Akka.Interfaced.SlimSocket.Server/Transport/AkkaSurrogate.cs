using System;
using Akka.Actor;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Akka.Interfaced.SlimSocket.Server
{
    public static class AkkaSurrogate
    {
        [ProtoContract]
        public class SurrogateForIActorRef
        {
            [ProtoMember(1)] public int Id;

            [ProtoConverter]
            public static SurrogateForIActorRef Convert(IActorRef value)
            {
                // used for sending an bound actor-ref to client.
                if (value == null)
                    return null;
                var actor = ((BoundActorRef)value);
                return new SurrogateForIActorRef { Id = actor.Id };
            }

            [ProtoConverter]
            public static IActorRef Convert(SurrogateForIActorRef value)
            {
                // not necessary because client cannot send IActorRef
                // but implemented to keep this class symmetrical.
                if (value == null)
                    return null;
                return new BoundActorRef(value.Id);
            }
        }

        [ProtoContract]
        public class SurrogateForINotificationChannel
        {
            [ProtoConverter]
            public static SurrogateForINotificationChannel Convert(INotificationChannel value)
            {
                if (value == null)
                    return null;
                return new SurrogateForINotificationChannel();
            }

            [ProtoConverter]
            public static INotificationChannel Convert(SurrogateForINotificationChannel value)
            {
                // always drop contents beacuse client and server dont share INotificationChannel
                return null;
            }
        }

        public static void Register(RuntimeTypeModel typeModel)
        {
            typeModel.Add(typeof(IActorRef), false).SetSurrogate(typeof(SurrogateForIActorRef));
            typeModel.Add(typeof(INotificationChannel), false).SetSurrogate(typeof(SurrogateForINotificationChannel));
        }
    }
}
