using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class UdpChannel : ChannelBase
    {
        private IPEndPoint _remoteEndPoint;
        private IPacketSerializer _packetSerializer;
        private NetPeerConfiguration _netConfig;
        private string _token;
        private NetClient _client;
        private Thread _clientThread;
        private ISlimTaskCompletionSource<bool> _connectTcs;

        public UdpChannel(ILog logger, IPEndPoint remoteEndPoint, string token, IPacketSerializer packetSerializer, NetPeerConfiguration netConfig)
            : base(logger)
        {
            _remoteEndPoint = remoteEndPoint;
            _token = token;
            _packetSerializer = packetSerializer;
            _netConfig = netConfig;
        }

        public override Task<bool> ConnectAsync()
        {
            if (State != ChannelStateType.Closed)
                throw new InvalidOperationException("Should be closed to connect.");

            var tcs = _connectTcs = TaskFactory.Create<bool>();
            _logger?.Info("Connect.");

            SetState(ChannelStateType.Connecting);

            _client = new NetClient(_netConfig);
            _client.Start();

            var hail = _client.CreateMessage();
            if (string.IsNullOrEmpty(_token) == false)
                hail.Write(_token);
            _client.Connect(_remoteEndPoint, hail);

            _clientThread = new Thread(ClientThreadWork);
            _clientThread.Start();

            return tcs.Task;
        }

        public override void Close()
        {
            _logger?.Info("Close.");

            SetState(ChannelStateType.Closed);
            _client?.Disconnect("Close");
        }

        private void SendPacket(Packet packet)
        {
            var msg = _client.CreateMessage();
            var workStream = new MemoryStream();
            _packetSerializer.Serialize(workStream, packet);
            msg.Write(workStream.GetBuffer(), 0, (int)workStream.Length);
            _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered, 0);
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnConnect()
        {
            _logger?.Trace("Connected.");

            SetConnected();
        }

        private void SetConnected()
        {
            SetState(ChannelStateType.Connected);

            if (_connectTcs != null)
            {
                _connectTcs.TrySetResult(true);
                _connectTcs = null;
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnReceive(object packet)
        {
            var p = (Packet)packet;
            OnPacket(p);
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnClose(string reason)
        {
            _logger?.TraceFormat("Closed. (reason={0})", reason);

            if (_connectTcs != null)
            {
                _connectTcs.TrySetException(new Exception(reason));
                _connectTcs = null;
            }

            SetState(ChannelStateType.Closed);
        }

        protected override void SendRequestPacket(Packet packet)
        {
            if (_state == ChannelStateType.Connected)
            {
                SendPacket(packet);
            }
        }

        private void ClientThreadWork()
        {
            var messages = new List<NetIncomingMessage>();

            while (_state != ChannelStateType.Closed)
            {
                var readed = _client.ReadMessages(messages);
                if (readed < 1)
                {
                    Thread.Sleep(10);
                    continue;
                }

                foreach (var msg in messages)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            var status = (NetConnectionStatus)msg.ReadByte();
                            var reason = msg.ReadString();
#if DEBUG
                            Console.WriteLine($"StatusChanged: Status={status} Reason={reason}");
#endif
                            if (status == NetConnectionStatus.Connected)
                            {
                                OnConnect();
                            }
                            else if (status == NetConnectionStatus.Disconnected)
                            {
                                OnClose(reason);
                            }
                            break;

                        case NetIncomingMessageType.Data:
                            var workStream = new MemoryStream(msg.ReadBytes(msg.LengthBytes), 0, msg.LengthBytes, false, true);
                            var packet = _packetSerializer.Deserialize(workStream);
                            OnReceive(packet);
                            break;
                    }
                }

                // recycle messages to avoid garbage
                _client.Recycle(messages);
                messages.Clear();
            }
        }
    }
}
