using System;
using System.Net.Sockets;
using Akka.Actor;
using Common.Logging;
using Akka.Event;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class TokenChecker : UntypedActor
    {
        private GatewayInitiator _initiator;
        private TcpGateway _gateway;
        private ILog _logger;
        private IActorRef _self;
        private EventStream _eventStream;
        private Socket _socket;
        private TcpConnection _connection;
        private ICancelable _timeoutCanceler;

        public TokenChecker(GatewayInitiator initiator, TcpGateway gateway, Socket socket)
        {
            _initiator = initiator;
            _gateway = gateway;
            _logger = initiator.CreateChannelLogger(socket.RemoteEndPoint, socket);
            _socket = socket;
            _connection = new TcpConnection(_logger) { Settings = initiator.ConnectionSettings };
        }

        protected override void PreStart()
        {
            base.PreStart();

            _self = Self;
            _eventStream = Context.System.EventStream;

            _connection.Closed += OnConnectionClose;
            _connection.Received += OnConnectionReceive;
            _connection.Open(_socket);

            if (_initiator.TokenTimeout != TimeSpan.Zero)
            {
                _timeoutCanceler = Context.System.Scheduler.ScheduleTellOnceCancelable(
                    _initiator.TokenTimeout, Self, PoisonPill.Instance, Self);
            }
        }

        protected override void PostStop()
        {
            if (_connection != null)
                _connection.Close();

            if (_timeoutCanceler != null)
                _timeoutCanceler.Cancel();

            base.PostStop();
        }

        protected override void OnReceive(object message)
        {
            Unhandled(message);
        }

        protected void OnConnectionClose(TcpConnection connection, int reason)
        {
            _self.Tell(PoisonPill.Instance);
        }

        protected void OnConnectionReceive(TcpConnection connection, object packet)
        {
            _connection.Closed -= OnConnectionClose;
            _connection.Received -= OnConnectionReceive;

            try
            {
                // Before token is validated

                var p = packet as Packet;
                if (p == null)
                {
                    _eventStream.Publish(new Warning(
                        _self.Path.ToString(), GetType(),
                        $"Receives null packet from {_connection?.RemoteEndPoint}"));
                    return;
                }

                if (p.Type != PacketType.System || (p.Message is string) == false)
                {
                    _eventStream.Publish(new Warning(
                        _self.Path.ToString(), GetType(),
                        $"Receives a bad packet without a message from {_connection?.RemoteEndPoint}"));
                    return;
                }

                // Try to open

                var token = (string)p.Message;
                var succeeded = _gateway.EstablishChannel(token, _connection);
                if (succeeded)
                {
                    // set null to avoid closing connection in PostStop
                    _connection = null;
                }
                else
                {
                    _eventStream.Publish(new Warning(
                        _self.Path.ToString(), GetType(),
                        $"Receives wrong token({token}) from {_connection?.RemoteEndPoint}"));
                    return;
                }
            }
            finally
            {
                // This actor will be destroyed anyway after this call.
                _self.Tell(PoisonPill.Instance);
            }
        }
    }
}
