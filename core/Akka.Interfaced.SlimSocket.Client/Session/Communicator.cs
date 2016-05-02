using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Akka.Interfaced.SlimSocket.Base;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    // TODO:
    // - Session management (Session ID issued by host. Rebind ression when reconnect)
    // - Full-Ordered Request (1 Request a time)

    public class Communicator
    {
        public enum StateType
        {
            None,
            Offline,
            Connecting,
            Connected,
            Paused,
            Stopped,
        }

        public StateType State => _state;
        public Action<SendOrPostCallback> ObserverEventPoster { get; set; }

        private volatile StateType _state;
        private readonly ILog _logger;
        private IPEndPoint _remoteEndPoint;
        private Func<Communicator, TcpConnection> _connectionFactory;
        private TcpConnection _tcpConnection;

        private int _lastRequestId = 0;
        private readonly List<Packet> _requestPacketQueues = new List<Packet>();
        private readonly ConcurrentDictionary<int, Action<ResponseMessage>> _requestResponseMap =
            new ConcurrentDictionary<int, Action<ResponseMessage>>();

        private int _lastObserverId;
        private readonly List<Packet> _recvSimplePackets = new List<Packet>();
        private readonly ConcurrentDictionary<int, ObserverEventDispatcher> _observerChannelMap =
            new ConcurrentDictionary<int, ObserverEventDispatcher>();

        public Communicator(ILog logger, IPEndPoint remoteEndPoint,
                            Func<Communicator, TcpConnection> connectionFactory)
        {
            _state = StateType.None;
            _logger = logger;
            _remoteEndPoint = remoteEndPoint;
            _connectionFactory = connectionFactory;
        }

        public void Start()
        {
            _logger.Info("Start.");
            _state = StateType.Offline;
            CreateNewConnect();
        }

        public void Stop()
        {
            _logger.Info("Stop.");
            _state = StateType.Stopped;
            _tcpConnection?.Close();
        }

        private void CreateNewConnect()
        {
            var connection = _connectionFactory(this);
            if (connection == null)
                throw new InvalidOperationException("Null connection is not allowed");

            _state = StateType.Connecting;
            _tcpConnection = connection;
            _tcpConnection.Connected += OnConnect;
            _tcpConnection.Received += OnReceive;
            _tcpConnection.Closed += OnClose;
            _tcpConnection.Connect(_remoteEndPoint);
        }

        public int IssueObserverId()
        {
            return ++_lastObserverId;
        }

        public void AddObserver(int observerId, ObserverEventDispatcher observer)
        {
            _observerChannelMap.TryAdd(observerId, observer);
        }

        public void RemoveObserver(int observerId)
        {
            ObserverEventDispatcher observer;
            _observerChannelMap.TryRemove(observerId, out observer);
        }

        public ObserverEventDispatcher GetObserver(int observerId)
        {
            ObserverEventDispatcher observer;
            return _observerChannelMap.TryGetValue(observerId, out observer)
                       ? observer
                       : null;
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnConnect(object sender)
        {
            _logger.Trace("Connected.");

            lock (_requestPacketQueues)
            {
                if (_requestPacketQueues.Count > 0)
                {
                    foreach (var packet in _requestPacketQueues)
                        _tcpConnection.SendPacket(packet);

                    _requestPacketQueues.Clear();
                }

                _state = StateType.Connected;
            }
        }

        // BEWARE: CALLED BY WORK THREAD
        private void OnReceive(object sender, object packet)
        {
            var p = (Packet)packet;
            switch (p.Type)
            {
                case PacketType.Notification:
                    var observer = GetObserver(p.ActorId);
                    if (observer == null)
                    {
                        _logger.WarnFormat("Notification didn't find observer. (ObserverId={0}, Message={1})",
                                           p.ActorId, p.Message.GetType().Name);
                        break;
                    }
                    if (ObserverEventPoster != null)
                        ObserverEventPoster(_ => observer.Invoke(p.RequestId, (IInvokable)p.Message));
                    else
                        observer.Invoke(p.RequestId, (IInvokable)p.Message);
                    break;

                case PacketType.Response:
                    Action<ResponseMessage> handler;
                    if (_requestResponseMap.TryRemove(p.RequestId, out handler))
                    {
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

        // BEWARE: CALLED BY WORK THREAD
        private void OnClose(object sender, int reason)
        {
            _logger.TraceFormat("Closed. (reason={0})", reason);

            _state = StateType.Offline;
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
                _requestResponseMap.TryAdd(packet.RequestId, completionHandler);

            if (_state == StateType.Connected)
            {
                _tcpConnection.SendPacket(packet);
            }
            else
            {
                lock (_requestPacketQueues)
                {
                    // double check state because state can be changed after first check
                    if (_state == StateType.Connected)
                        _tcpConnection.SendPacket(packet);
                    else
                        _requestPacketQueues.Add(packet);
                }
            }
        }
    }
}
