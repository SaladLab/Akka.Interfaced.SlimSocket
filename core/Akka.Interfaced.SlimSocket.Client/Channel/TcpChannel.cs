using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class TcpChannel : ChannelBase
    {
        private IPEndPoint _remoteEndPoint;
        private IPacketSerializer _packetSerializer;
        private string _token;
        private TcpConnection _tcpConnection;
        private ISlimTaskCompletionSource<bool> _connectTcs;

        public TcpChannel(ILog logger, IPEndPoint remoteEndPoint, string token, IPacketSerializer packetSerializer)
            : base(logger)
        {
            _remoteEndPoint = remoteEndPoint;
            _token = token;
            _packetSerializer = packetSerializer;
        }

        public override Task<bool> ConnectAsync()
        {
            if (State != ChannelStateType.Closed)
                throw new InvalidOperationException("Should be closed to connect.");

            var tcs = _connectTcs = TaskFactory.Create<bool>();
            _logger?.Info("Connect.");

            SetState(ChannelStateType.Connecting);

            var connection = new TcpConnection(_packetSerializer, _logger);
            _tcpConnection = connection;
            _tcpConnection.Connected += OnConnect;
            _tcpConnection.Received += OnReceive;
            _tcpConnection.Closed += OnClose;
            _tcpConnection.Connect(_remoteEndPoint);

            return tcs.Task;
        }

        public override void Close()
        {
            _logger?.Info("Close.");

            SetState(ChannelStateType.Closed);
            _tcpConnection?.Close();
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnConnect(object sender)
        {
            _logger?.Trace("Connected.");

            if (string.IsNullOrEmpty(_token))
            {
                SetConnected();
            }
            else
            {
                _tcpConnection.SendPacket(new Packet
                {
                    Type = PacketType.System,
                    Message = _token
                });

                SetState(ChannelStateType.TokenChecking);
            }
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
        private void OnReceive(object sender, object packet)
        {
            var p = (Packet)packet;
            if (p.Type == PacketType.System)
            {
                if (_state == ChannelStateType.TokenChecking)
                {
                    SetConnected();
                }
                else
                {
                    _logger.WarnFormat("System packet in wrong state({0}", _state);
                }
            }
            else
            {
                OnPacket(p);
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnClose(object sender, int reason)
        {
            _logger?.TraceFormat("Closed. (reason={0})", reason);

            if (_connectTcs != null)
            {
                _connectTcs.TrySetException(new SocketException(reason));
                _connectTcs = null;
            }

            SetState(ChannelStateType.Closed);
        }

        protected override void SendRequestPacket(Packet packet)
        {
            if (_state == ChannelStateType.Connected)
            {
                _tcpConnection.SendPacket(packet);
            }
        }
    }
}
