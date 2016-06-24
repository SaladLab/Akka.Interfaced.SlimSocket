using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Akka.Actor;
using Akka.Interfaced.SlimServer;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class UdpGateway : InterfacedActor, IGatewaySync, IActorBoundGatewaySync
    {
        private readonly GatewayInitiator _initiator;
        private readonly ILog _logger;
        private readonly IPacketSerializer _packetSerializer;
        private IActorRef _self;
        private NetServer _server;
        private Thread _serverThread;
        private readonly ConcurrentDictionary<NetConnection, IActorRef> _channelMap = new ConcurrentDictionary<NetConnection, IActorRef>();
        private readonly HashSet<IActorRef> _channelSet = new HashSet<IActorRef>();
        private bool _isStopped;

        internal class WaitingItem
        {
            public Tuple<IActorRef, TaggedType[], ActorBindingFlags> BindingActor;
            public DateTime Time;
        }

        private readonly Dictionary<string, WaitingItem> _waitingMap = new Dictionary<string, WaitingItem>();
        private ICancelable _timeoutCanceler;

        internal class AcceptMessage
        {
            public NetConnection SenderConnection { get; }
            public IPEndPoint SenderEndPoint { get; }
            public string Token { get; }

            public AcceptMessage(NetIncomingMessage message)
            {
                SenderConnection = message.SenderConnection;
                SenderEndPoint = message.SenderEndPoint;
                try
                {
                    Token = message.ReadString();
                }
                catch (Exception)
                {
                }
            }
        }

        private class TimeoutTimerMessage
        {
        }

        public UdpGateway(GatewayInitiator initiator)
        {
            _initiator = initiator;
            _logger = initiator.GatewayLogger;
            _packetSerializer = initiator.PacketSerializer;

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

            var udpConfig = (NetPeerConfiguration)_initiator.UdpConfig;
            udpConfig.LocalAddress = _initiator.ListenEndPoint.Address;
            udpConfig.Port = _initiator.ListenEndPoint.Port;
            udpConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            try
            {
                _self = Self;
                _server = new NetServer(udpConfig);
                _server.Start();
                _serverThread = new Thread(ServerThreadWork);
                _serverThread.Start();
            }
            catch (Exception e)
            {
                _logger?.ErrorFormat("Start got exception.", e);
            }
        }

        void IGatewaySync.Stop(bool stopListenOnly)
        {
            _logger?.Info($"Stop (StopListenOnly={stopListenOnly})");

            _isStopped = true;

            if (stopListenOnly)
            {
                _server.Configuration.AcceptIncomingConnections = false;
                return;
            }

            // stop listening and all running channels

            if (_server != null)
            {
                _server.Shutdown("ServerStop");
                _server = null;
            }

            if (_channelSet.Count == 0)
            {
                Context.Stop(Self);
            }
        }

        [MessageHandler]
        private void Handle(AcceptMessage m)
        {
            if (_isStopped)
                return;

            if (_initiator.CheckCreateChannel != null)
            {
                if (_initiator.CheckCreateChannel(m.SenderEndPoint, m.SenderConnection) == false)
                {
                    m.SenderConnection.Disconnect("Deny new connection");
                    return;
                }
            }

            IActorRef channel;
            if (_initiator.TokenRequired)
            {
                WaitingItem item;
                lock (_waitingMap)
                {
                    if (_waitingMap.TryGetValue(m.Token, out item) == false)
                    {
                        m.SenderConnection.Disconnect("Token not found");
                        return;
                    }
                    _waitingMap.Remove(m.Token);
                }

                channel = Context.ActorOf(Props.Create(() => new UdpChannel(_initiator, m.SenderConnection, item.BindingActor)));
            }
            else
            {
                channel = Context.ActorOf(Props.Create(() => new UdpChannel(_initiator, m.SenderConnection, null)));
            }

            if (channel == null)
            {
                m.SenderConnection.Deny("Server Deny");
                _logger?.ErrorFormat("Deny a connection. (EndPoint={0})", m.SenderEndPoint);
                return;
            }

            if (_channelMap.TryAdd(m.SenderConnection, channel) == false)
            {
                _logger?.ErrorFormat("Failed in adding new connection. (EndPoint={0})", m.SenderEndPoint);
                m.SenderConnection.Deny();
                channel.Tell(PoisonPill.Instance);
                return;
            }

            _logger?.TraceFormat("Accept a connection. (EndPoint={0})", m.SenderEndPoint);

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

        private void ServerThreadWork()
        {
            // capture _server member to avoid race condition.
            var server = _server;

            while (_isStopped == false || _channelMap.Count > 0)
            {
                NetIncomingMessage msg;
                while ((msg = server.WaitMessage(100)) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.ConnectionApproval:
                            _self.Tell(new AcceptMessage(msg));
                            break;

                        case NetIncomingMessageType.StatusChanged:
                            var status = (NetConnectionStatus)msg.ReadByte();
                            switch (status)
                            {
                                case NetConnectionStatus.Disconnected:
                                    IActorRef disconnectedChannel;
                                    if (_channelMap.TryRemove(msg.SenderConnection, out disconnectedChannel))
                                    {
                                        disconnectedChannel.Tell(UdpChannel.DisconnectedMessage.Instance);
                                    }
                                    else
                                    {
                                        _logger.ErrorFormat($"Failed in removing connection in Disconnected.");
                                    }
                                    break;
                            }
                            break;
                    }

                    server.Recycle(msg);
                }
            }
        }
    }
}
