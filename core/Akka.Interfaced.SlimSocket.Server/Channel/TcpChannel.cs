using System;
using System.Net.Sockets;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Interfaced.SlimServer;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class TcpChannel : ActorBoundChannelBase
    {
        private GatewayInitiator _initiator;
        private ILog _logger;
        private IActorRef _self;
        private EventStream _eventStream;
        private Socket _socket;
        private TcpConnection _connection;

        internal class CloseMessage
        {
            public static CloseMessage Instance = new CloseMessage();
        }

        public TcpChannel(GatewayInitiator initiator, Socket socket)
        {
            // open by client connection.
            _initiator = initiator;
            _logger = _initiator.CreateChannelLogger(socket.RemoteEndPoint, socket);
            _socket = socket;
            _connection = new TcpConnection(_logger) { Settings = initiator.ConnectionSettings };
        }

        public TcpChannel(GatewayInitiator initiator, TcpConnection connection, Tuple<IActorRef, TaggedType[], ActorBindingFlags> bindingActor)
        {
            // open by registerd token.
            _initiator = initiator;
            _logger = initiator.CreateChannelLogger(connection.RemoteEndPoint, connection.Socket);
            _socket = connection.Socket;
            _connection = connection;

            BindActor(bindingActor.Item1, bindingActor.Item2.Select(t => new BoundType(t)), bindingActor.Item3);
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;
            _eventStream = Context.System.EventStream;

            // create initial actors and bind them

            if (_initiator.CreateInitialActors != null)
            {
                var actors = _initiator.CreateInitialActors(Context, _socket);
                if (actors != null)
                {
                    foreach (var actor in actors)
                    {
                        BindActor(actor.Item1, actor.Item2.Select(t => new BoundType(t)));
                    }
                }
            }

            // link connection to this

            _connection.Closed += OnConnectionClose;
            _connection.Received += OnConnectionReceive;

            if (_connection.Socket == null)
            {
                try
                {
                    _connection.Open(_socket);
                }
                catch (Exception e)
                {
                    _logger.ErrorFormat("Cannot open connection.", e);
                }
            }
            else
            {
                if (_connection.Active)
                {
                    _connection.Send(new Packet
                    {
                        Type = PacketType.System,
                        Message = "1",
                    });
                }
                else
                {
                    OnConnectionClose(_connection, -1);
                }
            }
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

        [MessageHandler]
        private void Handle(CloseMessage m)
        {
             _connection.Close();
        }

        // BEWARE: Called by Network Thread
        private void OnConnectionClose(TcpConnection connection, int reason)
        {
            RunTask(() => Close(), _self);
        }

        // BEWARE: Called by Network Thread
        private void OnConnectionReceive(TcpConnection connection, object packet)
        {
            // The thread that call this function is different from actor context thread.
            // To deal with this contention lock protection is required.

            var p = packet as Packet;
            if (p == null)
            {
                _eventStream.Publish(new Warning(
                    _self.Path.ToString(), GetType(),
                    $"Receives null packet from {_connection?.RemoteEndPoint}"));
                return;
            }

            var msg = p.Message as IInterfacedPayload;
            if (msg == null)
            {
                _eventStream.Publish(new Warning(
                    _self.Path.ToString(), GetType(),
                    $"Receives a bad packet without a message from {_connection?.RemoteEndPoint}"));
                return;
            }

            var actor = GetBoundActor(p.ActorId);
            if (actor == null)
            {
                if (p.RequestId != 0)
                {
                    _connection.Send(new Packet
                    {
                        Type = PacketType.Response,
                        ActorId = p.ActorId,
                        RequestId = p.RequestId,
                        Message = null,
                        Exception = new RequestTargetException()
                    });
                }
                return;
            }

            var boundType = actor.FindBoundType(msg.GetInterfaceType());
            if (boundType == null)
            {
                if (p.RequestId != 0)
                {
                    _connection.Send(new Packet
                    {
                        Type = PacketType.Response,
                        ActorId = p.ActorId,
                        RequestId = p.RequestId,
                        Message = null,
                        Exception = new RequestHandlerNotFoundException()
                    });
                }
                return;
            }

            if (boundType.IsTagOverridable)
            {
                var tagOverridable = (IPayloadTagOverridable)p.Message;
                tagOverridable.SetTag(boundType.TagValue);
            }

            var observerUpdatable = p.Message as IPayloadObserverUpdatable;
            if (observerUpdatable != null)
            {
                observerUpdatable.Update(o =>
                {
                    var observer = (InterfacedObserver)o;
                    if (observer != null)
                        observer.Channel = new ActorNotificationChannel(_self);
                });
            }

            actor.Actor.Tell(new RequestMessage
            {
                RequestId = p.RequestId,
                InvokePayload = (IInterfacedPayload)p.Message
            }, _self);
        }
    }
}
