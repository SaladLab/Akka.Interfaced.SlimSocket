using System;
using System.IO;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class UdpChannel : ActorBoundChannelBase
    {
        private GatewayInitiator _initiator;
        private ILog _logger;
        private IActorRef _self;
        private EventStream _eventStream;
        private NetConnection _connection;
        private IPacketSerializer _packetSerializer;

        internal class DisconnectedMessage
        {
            public static DisconnectedMessage Instance = new DisconnectedMessage();
        }

        public UdpChannel(GatewayInitiator initiator, object connection, Tuple<IActorRef, TaggedType[], ActorBindingFlags> bindingActor = null)
        {
            var netConnection = (NetConnection)connection;
            _initiator = initiator;
            _logger = _initiator.CreateChannelLogger(netConnection.RemoteEndPoint, connection);
            _connection = netConnection;
            _packetSerializer = initiator.PacketSerializer;

            if (bindingActor != null)
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
                var actors = _initiator.CreateInitialActors(Context, _connection);
                if (actors != null)
                {
                    foreach (var actor in actors)
                    {
                        BindActor(actor.Item1, actor.Item2.Select(t => new BoundType(t)));
                    }
                }
            }

            // accept it

            _connection.MessageHandler = OnConnectionMessage;
            _connection.Approve();
        }

        protected override void PostStop()
        {
            _connection.Disconnect("Server Stop");

            base.PostStop();
        }

        private void SendPacket(Packet packet)
        {
            var msg = _connection.Peer.CreateMessage();
            var workStream = new MemoryStream();
            _packetSerializer.Serialize(workStream, packet);
            msg.Write(workStream.GetBuffer(), 0, (int)workStream.Length);
            _connection.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        protected override void OnNotificationMessage(NotificationMessage message)
        {
            SendPacket(new Packet
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
                SendPacket(new Packet
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

        protected override void OnCloseRequest()
        {
            _connection.Disconnect("Close");
        }

        [MessageHandler]
        private void Handle(DisconnectedMessage m)
        {
            Close();
        }

        private void OnConnectionReceive(Packet p)
        {
            // The thread that call this function is different from actor context thread.
            // To deal with this contention lock protection is required.

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
                    SendPacket(new Packet
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
                    SendPacket(new Packet
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

        // BEWARE: Called by network thread.
        private bool OnConnectionMessage(NetIncomingMessage msg)
        {
            if (msg.MessageType == NetIncomingMessageType.Data)
            {
                var workStream = new MemoryStream(msg.ReadBytes(msg.LengthBytes), 0, msg.LengthBytes, false, true);
                var packet = _packetSerializer.Deserialize(workStream) as Packet;
                if (packet != null)
                    OnConnectionReceive(packet);
                return true;
            }

            return false;
        }
    }
}
