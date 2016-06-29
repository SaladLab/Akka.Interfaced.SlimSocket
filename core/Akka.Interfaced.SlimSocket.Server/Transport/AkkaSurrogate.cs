using System;
using Akka.Actor;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Akka.Interfaced.SlimSocket.Server
{
    public static class AkkaSurrogate
    {
        [ProtoContract]
        public class SurrogateForIRequestTarget
        {
            [ProtoMember(1)] public int Id;
            [ProtoMember(2)] public string Address;

            [ProtoConverter]
            public static SurrogateForIRequestTarget Convert(IRequestTarget value)
            {
                // used for sending an bound actor-ref to client.
                if (value == null)
                    return null;
                var target = ((BoundActorTarget)value);
                return new SurrogateForIRequestTarget { Id = target.Id, Address = target.Address };
            }

            [ProtoConverter]
            public static IRequestTarget Convert(SurrogateForIRequestTarget value)
            {
                // not necessary because client cannot send IRequestTarget
                // but implemented to keep this class symmetrical.
                if (value == null)
                    return null;
                return new BoundActorTarget(value.Id, value.Address);
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
            typeModel.Add(typeof(IRequestTarget), false).SetSurrogate(typeof(SurrogateForIRequestTarget));
            typeModel.Add(typeof(INotificationChannel), false).SetSurrogate(typeof(SurrogateForINotificationChannel));
        }
    }
}
