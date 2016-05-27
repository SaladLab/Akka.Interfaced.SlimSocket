using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using Akka.Actor;
using Akka.Interfaced.SlimSocket.Base;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class ClientSession : ActorBoundSession
    {
        private ILog _logger;
        private IActorRef _self;
        private TcpConnection _connection;
        private Socket _socket;
        private Func<IActorContext, Socket, Tuple<IActorRef, Type>[]> _initialActorFactory;

        public ClientSession(ILog logger, Socket socket, TcpConnectionSettings connectionSettings,
                             Func<IActorContext, Socket, Tuple<IActorRef, Type>[]> initialActorFactory)
        {
            _logger = logger;
            _socket = socket;
            _connection = new TcpConnection(logger) { Settings = connectionSettings };
            _initialActorFactory = initialActorFactory;
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;

            var actors = _initialActorFactory(Context, _socket);
            if (actors != null)
            {
                foreach (var actor in actors)
                    BindActor(actor.Item1, actor.Item2);
            }

            _connection.Closed += OnConnectionClose;
            _connection.Received += OnConnectionReceive;

            if (_socket != null)
                _connection.Open(_socket);
        }

        protected override void PostStop()
        {
            _connection.Close();

            base.PostStop();
        }

        protected override void OnNotificationMessage(NotificationMessage message)
        {
            _connection.Send(new Packet
            {
                Type = PacketType.Notification,
                ActorId = message.ObserverId,
                RequestId = message.NotificationId,
                Message = message.InvokePayload,
            });
        }

        protected override void OnResponseMessage(ResponseMessage message)
        {
            var actorId = GetBoundActorId(Sender);
            if (actorId != 0)
            {
                _connection.Send(new Packet
                {
                    Type = PacketType.Response,
                    ActorId = actorId,
                    RequestId = message.RequestId,
                    Message = message.ReturnPayload,
                    Exception = message.Exception
                });
            }
            else
            {
                _logger.WarnFormat("Not bound actorId owned by ReponseMessage. (ActorId={0})", actorId);
            }
        }

        protected void OnConnectionClose(TcpConnection connection, int reason)
        {
            // TODO: need to implement temporary live session for rebinding in short time reconnection.
            //       but now just stop session

            _self.Tell(PoisonPill.Instance);
        }

        protected void OnConnectionReceive(TcpConnection connection, object packet)
        {
            // The thread that call this function is different from actor context thread.
            // To deal with this contention lock protection is required.

            var p = packet as Packet;

            if (p == null || p.Message == null)
            {
                return;
            }

            var actor = GetBoundActor(p.ActorId);
            if (actor != null)
            {
                if (actor.InterfaceType != null)
                {
                    var msg = (IInterfacedPayload)p.Message;
                    if (msg == null || msg.GetInterfaceType() != actor.InterfaceType)
                    {
                        Console.WriteLine("Got packet but weired! {0}", msg.GetType());
                        return;
                    }
                }

                if (actor.IsTagOverridable)
                {
                    var msg = (IPayloadTagOverridable)p.Message;
                    msg.SetTag(actor.TagValue);
                }

                var observerUpdatable = p.Message as IPayloadObserverUpdatable;
                if (observerUpdatable != null)
                {
                    observerUpdatable.Update(o => ((InterfacedObserver)o).Channel = new ActorNotificationChannel(_self));
                }

                actor.Actor.Tell(new RequestMessage
                {
                    RequestId = p.RequestId,
                    InvokePayload = (IInterfacedPayload)p.Message
                }, _self);
            }
        }
    }
}
