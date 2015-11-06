using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Akka.Interfaced.SlimSocket.Base;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class Communicator
    {
        private readonly ILog _logger;
        private IPEndPoint _remoteEndPoint;
        private Func<Communicator, TcpConnection> _connectionFactory;
        private TcpConnection _tcpConnection;
        private int _lastRequestId = 0;
        private List<Packet> _requestPackets = new List<Packet>();
        private Dictionary<int, Action<ResponseMessage>> _requestResponseMap = new Dictionary<int, Action<ResponseMessage>>();

        // Durable Connection
        // 

        public Communicator(ILog logger, IPEndPoint remoteEndPoint, Func<Communicator, TcpConnection> connectionFactory)
        {
            _logger = logger;
            _remoteEndPoint = remoteEndPoint;
            _connectionFactory = connectionFactory;
        }

        public void Start()
        {
            _logger.Info("Start");
            CreateNewConnect();
        }

        public void Stop()
        {
            _logger.Info("Stop");
            _tcpConnection.Close();
        }

        private void CreateNewConnect()
        {
            var connection = _connectionFactory(this);
            if (connection == null)
                throw new InvalidOperationException("Null connection is not allowed");

            _tcpConnection = connection;
            _tcpConnection.Connected += OnConnection;
            _tcpConnection.Received += OnRecvPacket;
            _tcpConnection.Closed += OnClose;
            _tcpConnection.Connect(_remoteEndPoint);
        }

        private void OnConnection(object sender)
        {
            _logger.Trace("Connection Connected.");
        }

        private void OnRecvPacket(object sender, object packet)
        {
            var p = (Packet)packet;
            switch (p.Type)
            {
                case PacketType.Notification:
                    // lock (_recvSimplePackets)
                    //    _recvSimplePackets.Add(p);
                    break;

                case PacketType.Response:
                    Action<ResponseMessage> handler;
                    if (_requestResponseMap.TryGetValue(p.RequestId, out handler))
                    {
                        _requestResponseMap.Remove(p.RequestId);
                        handler(new ResponseMessage
                        {
                            RequestId = p.RequestId,
                            ReturnPayload = (IValueGetable)p.Message,
                            Exception = p.Exception
                        });
                    }
                    break;
            }
        }

        private void OnClose(object sender, int reason)
        {
            _logger.Trace("OnClose reason=" + reason);
        }

        public void SendRequest(IActorRef target, RequestMessage requestMessage)
        {
            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((SlimActorRef)target).Id,
                Message = requestMessage.InvokePayload,
            }, null);
        }

        public void SendRequestAndWait(ISlimTaskCompletionSource<bool> tcs,
                                         IActorRef target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((SlimActorRef)target).Id,
                Message = requestMessage.InvokePayload,
            }, r =>
            {
                if (r.Exception != null)
                    tcs.SetException(r.Exception);
                else
                    tcs.SetResult(true);
            });
        }

        public void SendRequestAndReceive<TResult>(ISlimTaskCompletionSource<TResult> tcs,
                                                     IActorRef target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((SlimActorRef)target).Id,
                Message = requestMessage.InvokePayload,
            }, r =>
            {
                if (r.Exception != null)
                    tcs.SetException(r.Exception);
                else if (r.ReturnPayload != null)
                    tcs.SetResult((TResult)(r.ReturnPayload.Value));
                else
                    tcs.SetException(new InvalidOperationException("No exception and result. Weird?"));
            });
        }

        private void SendRequestPacket(Packet packet, Action<ResponseMessage> completionHandler)
        {
            packet.RequestId = ++_lastRequestId;

            if (completionHandler != null)
                _requestResponseMap.Add(packet.RequestId, completionHandler);

            // TODO: Pending if not connected

            _tcpConnection.SendPacket(packet);
        }
    }
}
