using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Linq;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class TcpGateway : InterfacedActor, IGatewaySync, IActorBoundGatewaySync
    {
        private readonly GatewayInitiator _initiator;
        private readonly ILog _logger;
        private IActorRef _self;
        private TcpAcceptor _tcpAcceptor;
        private readonly HashSet<IActorRef> _channelSet = new HashSet<IActorRef>();
        private bool _isStopped;

        internal class WaitingItem
        {
            public object Tag;
            public Tuple<IActorRef, TaggedType[], ActorBindingFlags> BindingActor;
            public DateTime Time;
        }

        private readonly Dictionary<string, WaitingItem> _waitingMap = new Dictionary<string, WaitingItem>();
        private ICancelable _timeoutCanceler;

        private class AcceptMessage
        {
            public Socket Socket { get; }

            public AcceptMessage(Socket socket)
            {
                Socket = socket;
            }
        }

        private class AcceptByTokenMessage
        {
            public TcpConnection Connection { get; }
            public object Tag;
            public Tuple<IActorRef, TaggedType[], ActorBindingFlags> BindingActor { get; }

            public AcceptByTokenMessage(TcpConnection connection, object tag, Tuple<IActorRef, TaggedType[], ActorBindingFlags> bindingActor)
            {
                Connection = connection;
                Tag = tag;
                BindingActor = bindingActor;
            }
        }

        private class TimeoutTimerMessage
        {
        }

        public TcpGateway(GatewayInitiator initiator)
        {
            _initiator = initiator;
            _logger = initiator.GatewayLogger;

            if (initiator.TokenRequired && initiator.TokenTimeout != TimeSpan.Zero)
            {
                _timeoutCanceler = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    initiator.TokenTimeout, initiator.TokenTimeout, Self, new TimeoutTimerMessage(), Self);
            }
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;
            _initiator.GatewayInitialized?.Invoke(_self);
        }

        protected override void PostStop()
        {
            base.PostStop();

            if (_timeoutCanceler != null)
                _timeoutCanceler.Cancel();
        }

        void IGatewaySync.Start()
        {
            _logger?.InfoFormat("Start (EndPoint={0})", _initiator.ListenEndPoint);

            try
            {
                _tcpAcceptor = new TcpAcceptor();
                _tcpAcceptor.Accepted += OnConnectionAccept;
                _tcpAcceptor.Listen(_initiator.ListenEndPoint);
            }
            catch (Exception e)
            {
                _logger?.ErrorFormat("Start got exception.", e);
            }
        }

        void IGatewaySync.Stop(bool stopListenOnly)
        {
            _logger?.Info($"Stop (StopListenOnly={stopListenOnly})");

            // stop listening

            _isStopped = true;

            if (_tcpAcceptor != null)
            {
                _tcpAcceptor.Close();
                _tcpAcceptor = null;
            }

            if (stopListenOnly)
                return;

            // stop all running channels

            _isStopped = true;

            if (_channelSet.Count > 0)
            {
                foreach (var channel in _channelSet)
                    channel.Cast<ActorBoundChannelRef>().WithNoReply().Close();
            }
            else
            {
                Self.Tell(InterfacedPoisonPill.Instance);
            }
        }

        private TcpAcceptor.AcceptResult OnConnectionAccept(TcpAcceptor sender, Socket socket)
        {
            _self.Tell(new AcceptMessage(socket), _self);
            return TcpAcceptor.AcceptResult.Accept;
        }

        [MessageHandler]
        private void Handle(AcceptMessage m)
        {
            if (_isStopped)
                return;

            if (_initiator.CheckCreateChannel != null)
            {
                if (_initiator.CheckCreateChannel(m.Socket.RemoteEndPoint, m.Socket) == false)
                {
                    m.Socket.Close();
                    return;
                }
            }

            if (_initiator.TokenRequired)
            {
                Context.ActorOf(Props.Create(() => new TokenChecker(_initiator, this, m.Socket)));
            }
            else
            {
                var channel = Context.ActorOf(Props.Create(() => new TcpChannel(_initiator, m.Socket, null)));
                if (channel == null)
                {
                    _logger?.TraceFormat("Deny a connection. (EndPoint={0})", m.Socket.RemoteEndPoint);
                    return;
                }

                _logger?.TraceFormat("Accept a connection. (EndPoint={0})", m.Socket.RemoteEndPoint);

                Context.Watch(channel);
                _channelSet.Add(channel);
            }
        }

        [MessageHandler]
        private void Handle(AcceptByTokenMessage m)
        {
            if (_isStopped)
                return;

            if (_initiator.CheckCreateChannel != null)
            {
                if (_initiator.CheckCreateChannel(m.Connection.RemoteEndPoint, m.Connection.Socket) == false)
                {
                    m.Connection.Close();
                    return;
                }
            }

            var channel = Context.ActorOf(Props.Create(() => new TcpChannel(_initiator, m.Connection, m.Tag, m.BindingActor)));
            if (channel == null)
            {
                _logger?.TraceFormat("Deny a connection. (EndPoint={0})", m.Connection.RemoteEndPoint);
                return;
            }

            _logger?.TraceFormat("Accept a connection. (EndPoint={0})", m.Connection.RemoteEndPoint);

            Context.Watch(channel);
            _channelSet.Add(channel);
        }

        [ResponsiveExceptionAll]
        InterfacedActorRef IActorBoundGatewaySync.OpenChannel(InterfacedActorRef actor, object tag, ActorBindingFlags bindingFlags)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            var targetActor = ((AkkaActorTarget)actor.Target).Actor;
            var target = ((IActorBoundGatewaySync)this).OpenChannel(targetActor, new TaggedType[] { actor.InterfaceType }, bindingFlags);

            var actorRef = (InterfacedActorRef)Activator.CreateInstance(actor.GetType());
            InterfacedActorRefModifier.SetTarget(actorRef, target);
            return actorRef;
        }

        [ResponsiveExceptionAll]
        BoundActorTarget IActorBoundGatewaySync.OpenChannel(IActorRef actor, TaggedType[] types, object tag, ActorBindingFlags bindingFlags)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            if (_isStopped)
                return null;

            // create token and add to waiting list

            string token;
            while (true)
            {
                token = Guid.NewGuid().ToString();
                lock (_waitingMap)
                {
                    if (_waitingMap.ContainsKey(token) == false)
                    {
                        _waitingMap.Add(token, new WaitingItem
                        {
                            Tag = tag,
                            BindingActor = Tuple.Create(actor, types, bindingFlags),
                            Time = DateTime.UtcNow
                        });
                        break;
                    }
                }
            }

            var address = string.Join("|", ChannelType.Tcp.ToString(),
                                           _initiator.ConnectEndPoint.ToString(),
                                           token);
            return new BoundActorTarget(1, address);
        }

        // Called by Another Worker Threads
        internal bool EstablishChannel(string token, TcpConnection connection)
        {
            WaitingItem item;

            lock (_waitingMap)
            {
                if (_waitingMap.TryGetValue(token, out item) == false)
                    return false;

                _waitingMap.Remove(token);
            }

            _self.Tell(new AcceptByTokenMessage(connection, item.Tag, item.BindingActor), _self);
            return true;
        }

        [MessageHandler]
        private void Handle(TimeoutTimerMessage m)
        {
            lock (_waitingMap)
            {
                var now = DateTime.UtcNow;
                var timeoutItems = _waitingMap.Where(i => (now - i.Value.Time) > _initiator.TokenTimeout).ToList();
                foreach (var i in timeoutItems)
                {
                    _waitingMap.Remove(i.Key);
                    if (i.Value.BindingActor.Item3.HasFlag(ActorBindingFlags.OpenThenNotification))
                    {
                        i.Value.BindingActor.Item1.Tell(new NotificationMessage
                        {
                            InvokePayload = new IActorBoundChannelObserver_PayloadTable.ChannelOpenTimeout_Invoke
                            {
                                tag = i.Value.Tag
                            },
                        });
                    }
                }
            }
        }

        [MessageHandler]
        private void Handle(Terminated m)
        {
            _channelSet.Remove(m.ActorRef);

            if (_isStopped && _channelSet.Count == 0)
                Self.Tell(InterfacedPoisonPill.Instance);
        }
    }
}
