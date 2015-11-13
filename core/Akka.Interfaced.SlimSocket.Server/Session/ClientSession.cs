﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using Akka.Actor;
using Akka.Interfaced.SlimSocket.Base;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    // SlimClient 로 들어온 모든 요청은 ClientSession 을 통해 최종 Actor 에게 전달되며
    // 그 요청에 대한 결과도 이것을 통해 SlimClient 에게 전달된다.
    // - BoundActor 관리
    // - Connection 관리
    public class ClientSession : UntypedActor
    {
        private ILog _logger;
        private IActorRef _self;
        private TcpConnection _connection;
        private Socket _socket;
        private Func<IActorContext, Socket, Tuple<IActorRef, Type>[]> _initialActorFactory;

        private class BoundActorItem
        {
            public IActorRef Actor;
            public Type InterfaceType;
            public bool IsTagOverridable;
            public object TagValue;
        }

        private object _boundActorLock = new object();
        private Dictionary<int, BoundActorItem> _boundActorMap;
        private Dictionary<IActorRef, int> _boundActorInverseMap;
        private int _lastBoundActorId;

        public ClientSession(ILog logger, Socket socket, TcpConnectionSettings connectionSettings,
                             Func<IActorContext, Socket, Tuple<IActorRef, Type>[]> initialActorFactory)
        {
            _logger = logger;
            _socket = socket;
            _connection = new TcpConnection(logger) { Settings = connectionSettings };
            _initialActorFactory = initialActorFactory;
            _boundActorMap = new Dictionary<int, BoundActorItem>();
            _boundActorInverseMap = new Dictionary<IActorRef, int>();
        }

        protected override void PreStart()
        {
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

            lock (_boundActorLock)
            {
                foreach (var boundActor in _boundActorMap)
                    boundActor.Value.Actor.Tell(new ClientSessionMessage.BoundSessionTerminated());
            }
        }

        protected override void OnReceive(object message)
        {
            var notificationMessage = message as NotificationMessage;
            if (notificationMessage != null)
            {
                _connection.Send(new Packet
                {
                    Type = PacketType.Notification,
                    ActorId = notificationMessage.ObserverId,
                    RequestId = notificationMessage.NotificationId,
                    Message = notificationMessage.InvokePayload,
                });
                return;
            }

            var response = message as ResponseMessage;
            if (response != null)
            {
                var actorId = GetBoundActorId(Sender);
                if (actorId != 0)
                {
                    // TODO: Sender 에 접근하지 않고 ActorId 를 얻을 수 있도록 하자 (성능 이슈)
                    _connection.Send(new Packet
                    {
                        Type = PacketType.Response,
                        ActorId = actorId,
                        RequestId = response.RequestId,
                        Message = response.ReturnPayload,
                        Exception = response.Exception
                    });
                }
                return;
            }

            var bindActorRequestMessage = message as ClientSessionMessage.BindActorRequest;
            if (bindActorRequestMessage != null)
            {
                var actorId = BindActor(
                    bindActorRequestMessage.Actor,
                    bindActorRequestMessage.InterfaceType,
                    bindActorRequestMessage.TagValue);
                Sender.Tell(new ClientSessionMessage.BindActorResponse { ActorId = actorId });
                return;
            }

            var unbindActorRequestMessage = message as ClientSessionMessage.UnbindActorRequest;
            if (unbindActorRequestMessage != null)
            {
                if (unbindActorRequestMessage.Actor != null)
                    UnbindActor(unbindActorRequestMessage.Actor);
                else if (unbindActorRequestMessage.ActorId != 0)
                    UnbindActor(unbindActorRequestMessage.ActorId);
                return;
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
                    var msg = (ITagOverridable)p.Message;
                    msg.SetTag(actor.TagValue);
                }

                actor.Actor.Tell(new RequestMessage
                {
                    RequestId = p.RequestId,
                    InvokePayload = (IAsyncInvokable)p.Message
                }, _self);
            }
        }

        #region BoundActor

        private int BindActor(IActorRef actor, Type interfaceType, object tagValue = null)
        {
            lock (_boundActorLock)
            {
                var actorId = ++_lastBoundActorId;
                _boundActorMap[actorId] = new BoundActorItem
                {
                    Actor = actor,
                    InterfaceType = interfaceType,
                    IsTagOverridable = interfaceType != null &&
                                       interfaceType.GetCustomAttribute<TagOverridableAttribute>() != null,
                    TagValue = tagValue
                };
                _boundActorInverseMap[actor] = actorId;
                return actorId;
            }
        }

        private BoundActorItem GetBoundActor(int id)
        {
            lock (_boundActorLock)
            {
                BoundActorItem item;
                return _boundActorMap.TryGetValue(id, out item) ? item : null;
            }
        }

        private int GetBoundActorId(IActorRef actor)
        {
            lock (_boundActorLock)
            {
                int actorId;
                return _boundActorInverseMap.TryGetValue(actor, out actorId) ? actorId : 0;
            }
        }

        private void UnbindActor(IActorRef actor)
        {
            lock (_boundActorLock)
            {
                int actorId;
                if (_boundActorInverseMap.TryGetValue(actor, out actorId))
                {
                    _boundActorMap.Remove(actorId);
                    _boundActorInverseMap.Remove(actor);
                }
            }
        }

        private void UnbindActor(int actorId)
        {
            lock (_boundActorLock)
            {
                BoundActorItem item;
                if (_boundActorMap.TryGetValue(actorId, out item))
                {
                    _boundActorMap.Remove(actorId);
                    _boundActorInverseMap.Remove(item.Actor);
                }
            }
        }

        #endregion
    }
}
