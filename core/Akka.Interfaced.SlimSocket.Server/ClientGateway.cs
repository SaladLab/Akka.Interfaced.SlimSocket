using System;
using System.Net.Sockets;
using System.Collections.Generic;
using Akka.Actor;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Server
{
    public class ClientGateway : ReceiveActor
    {
        private readonly ILog _logger;
        private readonly Func<Socket, IActorRef> _clientSessionCreator;
        private TcpAcceptor _tcpAcceptor;
        private readonly HashSet<IActorRef> _sessionSet = new HashSet<IActorRef>();
        private bool _isStopped;

        public ClientGateway(ILog logger, Func<Socket, IActorRef> clientSessionCreator)
        {
            _logger = logger;
            _clientSessionCreator = clientSessionCreator;

            Receive<ClientGatewayMessage.Start>(m => Handle(m));
            Receive<ClientGatewayMessage.Accept>(m => Handle(m));
            Receive<ClientGatewayMessage.Shutdown>(m => Handle(m));
            Receive<Terminated>(m => Handle(m));
        }

        private void Handle(ClientGatewayMessage.Start m)
        {
            _logger?.InfoFormat("Start Listening. (EndPoint={0})", m.ServiceEndPoint);

            try
            {
                var self = Self;
                _tcpAcceptor = new TcpAcceptor();
                _tcpAcceptor.Accepted += (sender, socket) =>
                {
                    self.Tell(new ClientGatewayMessage.Accept { Socket = socket }, self);
                    return TcpAcceptor.AcceptResult.Accept;
                };
                _tcpAcceptor.Listen(m.ServiceEndPoint);
            }
            catch (Exception e)
            {
                _logger?.ErrorFormat("Start got exception.", e);
            }
        }

        private void Handle(ClientGatewayMessage.Accept m)
        {
            if (_isStopped)
                return;

            var clientSession = _clientSessionCreator(m.Socket);
            if (clientSession == null)
            {
                _logger?.TraceFormat("Deny a connection. (EndPoint={0})", m.Socket.RemoteEndPoint);
                return;
            }

            _logger?.TraceFormat("Accept a connection. (EndPoint={0})", m.Socket.RemoteEndPoint);

            Context.Watch(clientSession);
            _sessionSet.Add(clientSession);
        }

        private void Handle(ClientGatewayMessage.Shutdown m)
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

            // stop all running client sessions

            if (_sessionSet.Count > 0)
            {
                Context.ActorSelection("*").Tell(PoisonPill.Instance);
            }
            else
            {
                Context.Stop(Self);
            }
        }

        private void Handle(Terminated m)
        {
            _sessionSet.Remove(m.ActorRef);

            if (_isStopped && _sessionSet.Count == 0)
                Context.Stop(Self);
        }
    }
}
