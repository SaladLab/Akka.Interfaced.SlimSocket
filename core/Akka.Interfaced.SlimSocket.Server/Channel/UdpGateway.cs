﻿using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Akka.Actor;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class UdpGateway : ReceiveActor
    {
        private readonly GatewayInitiator _initiator;
        private readonly ILog _logger;
        private readonly IPacketSerializer _packetSerializer;
        private IActorRef _self;
        private NetServer _server;
        private Thread _serverThread;
        private bool _isStopped;

        internal class ChannelItem
        {
            public IActorRef Actor;
            public UdpChannel Channel;
        }

        private readonly ConcurrentDictionary<NetConnection, ChannelItem> _channelMap = new ConcurrentDictionary<NetConnection, ChannelItem>();

        internal class WaitingItem
        {
            public ActorBoundGatewayMessage.Open Message;
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

            Receive<GatewayMessage.Start>(m => Handle(m));
            Receive<GatewayMessage.Stop>(m => Handle(m));
            Receive<AcceptMessage>(m => Handle(m));
            Receive<ActorBoundGatewayMessage.Open>(m => Handle(m));
            Receive<TimeoutTimerMessage>(m => Handle(m));
            // Receive<Terminated>(m => Handle(m));

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

        private void Handle(GatewayMessage.Start m)
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

        private void Handle(GatewayMessage.Stop m)
        {
            if (_isStopped)
                return;

            _logger?.Info("Stop listening.");
            _isStopped = true;

            // stop listening

            if (_server != null)
            {
                _server.Shutdown("ServerStop");
                _server = null;
            }

            // stop all running client sessions

            if (_channelMap.Count > 0)
            {
                Context.ActorSelection("*").Tell(PoisonPill.Instance);
            }
            else
            {
                Context.Stop(Self);
            }
        }

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

            var channelItem = new ChannelItem();

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

                channelItem.Actor = Context.ActorOf(Props.Create(() => new UdpChannel(_initiator, m.SenderConnection, channelItem, item.Message)));
            }
            else
            {
                channelItem.Actor = Context.ActorOf(Props.Create(() => new UdpChannel(_initiator, m.SenderConnection, channelItem, null)));
            }

            if (channelItem.Actor == null)
            {
                m.SenderConnection.Deny("Server Deny");
                _logger?.TraceFormat("Deny a connection. (EndPoint={0})", m.SenderEndPoint);
                return;
            }

            if (_channelMap.TryAdd(m.SenderConnection, channelItem) == false)
            {
                _logger?.ErrorFormat("Failed in adding new connection. (EndPoint={0})", m.SenderEndPoint);
                m.SenderConnection.Deny();
                channelItem.Actor.Tell(PoisonPill.Instance);
                return;
            }

            _logger?.TraceFormat("Accept a connection. (EndPoint={0})", m.SenderEndPoint);
        }

        private void Handle(ActorBoundGatewayMessage.Open m)
        {
            if (_isStopped)
            {
                Sender.Tell(new ActorBoundGatewayMessage.OpenReply(null));
                return;
            }

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
                            Message = m,
                            Time = DateTime.UtcNow
                        });
                        break;
                    }
                }
            }

            var address = string.Join("|", _initiator.ConnectEndPoint.Address.ToString(),
                                           _initiator.ConnectEndPoint.Port.ToString(),
                                           token);
            Sender.Tell(new ActorBoundGatewayMessage.OpenReply(address));
        }

        private void Handle(TimeoutTimerMessage m)
        {
            lock (_waitingMap)
            {
                var now = DateTime.UtcNow;
                var timeoutKeys = _waitingMap.Where(i => (now - i.Value.Time) > _initiator.TokenTimeout).Select(i => i.Key);
                timeoutKeys.ToList().ForEach(i => _waitingMap.Remove(i));
            }
        }

        private void ServerThreadWork()
        {
            // capture _server member to avoid race condition.
            var server = _server;

            while (_isStopped == false)
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
#if DEBUG
                            Console.WriteLine($"StatusChanged: Status={status}");
#endif
                            switch (status)
                            {
                                case NetConnectionStatus.Disconnected:
                                    ChannelItem disconnectedChannel;
                                    if (_channelMap.TryRemove(msg.SenderConnection, out disconnectedChannel))
                                    {
                                        disconnectedChannel.Actor.Tell(new UdpChannel.CloseMessage());
                                    }
                                    else
                                    {
                                        _logger.ErrorFormat($"Failed in removing connection in Disconnected.");
                                    }
                                    break;
                            }
                            break;

                        case NetIncomingMessageType.Data:
#if DEBUG
                            Console.WriteLine($"Data: Length={msg.LengthBytes}");
#endif
                            ChannelItem dataChannel = null;
                            if (_channelMap.TryGetValue(msg.SenderConnection, out dataChannel))
                            {
                                var workStream = new MemoryStream(msg.ReadBytes(msg.LengthBytes), 0, msg.LengthBytes, false, true);
                                var packet = _packetSerializer.Deserialize(workStream) as Packet;
                                if (packet != null)
                                    dataChannel.Channel.OnConnectionReceive(packet);
                            }
                            break;
                    }

                    server.Recycle(msg);
                }
            }
        }
    }
}
