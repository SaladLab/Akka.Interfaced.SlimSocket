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
            public Tuple<IActorRef, TaggedType[], ActorBindingFlags> BindingActor { get; }

            public AcceptByTokenMessage(TcpConnection connection, Tuple<IActorRef, TaggedType[], ActorBindingFlags> bindingActor)
            {
                Connection = connection;
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
            _logger?.InfoFormat("Start Listening. (EndPoint={0})", _initiator.ListenEndPoint);

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

        void IGatewaySync.Stop()
        {
            if (_isStopped)
                return;

            _logger?.Info("Stop listening.");
            _isStopped = true;

            // stop listening

            if (_tcpAcceptor != null)
            {
                _tcpAcceptor.Close();
                _tcpAcceptor = null;
            }

            // stop all running channels

            if (_channelSet.Count > 0)
            {
                Context.ActorSelection("*").Tell(PoisonPill.Instance);
            }
            else
            {
                Context.Stop(Self);
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
                var channel = Context.ActorOf(Props.Create(() => new TcpChannel(_initiator, m.Socket)));
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

            var channel = Context.ActorOf(Props.Create(() => new TcpChannel(_initiator, m.Connection, m.BindingActor)));
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
        string IActorBoundGatewaySync.OpenChannel(InterfacedActorRef actor, ActorBindingFlags bindingFlags)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            var targetActor = ((AkkaActorTarget)actor.Target).Actor;
            return ((IActorBoundGatewaySync)this).OpenChannel(targetActor, new TaggedType[] { actor.InterfaceType }, bindingFlags);
        }

        [ResponsiveExceptionAll]
        string IActorBoundGatewaySync.OpenChannel(IActorRef actor, TaggedType[] types, ActorBindingFlags bindingFlags)
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
                            BindingActor = Tuple.Create(actor, types, bindingFlags),
                            Time = DateTime.UtcNow
                        });
                        break;
                    }
                }
            }

            var address = string.Join("|", _initiator.ConnectEndPoint.Address.ToString(),
                                           _initiator.ConnectEndPoint.Port.ToString(),
                                           token);
            return address;
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

            _self.Tell(new AcceptByTokenMessage(connection, item.BindingActor), _self);
            return true;
        }

        [MessageHandler]
        private void Handle(TimeoutTimerMessage m)
        {
            lock (_waitingMap)
            {
                var now = DateTime.UtcNow;
                var timeoutKeys = _waitingMap.Where(i => (now - i.Value.Time) > _initiator.TokenTimeout).Select(i => i.Key);
                timeoutKeys.ToList().ForEach(i => _waitingMap.Remove(i));
            }
        }

        [MessageHandler]
        private void Handle(Terminated m)
        {
            _channelSet.Remove(m.ActorRef);

            if (_isStopped && _channelSet.Count == 0)
                Context.Stop(Self);
        }
    }
}
