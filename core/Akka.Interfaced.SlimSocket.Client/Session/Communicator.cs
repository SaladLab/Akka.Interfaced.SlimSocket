using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Interfaced.SlimSocket.Base;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    // TODO:
    // - Session management (Session ID issued by host. Rebind ression when reconnect)
    // - Full-Ordered Request (1 Request a time)

    public class Communicator : IRequestWaiter
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

        public ISlimTaskFactory TaskFactory { get; set; }
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
        private readonly ConcurrentDictionary<int, InterfacedObserver> _observerChannelMap =
            new ConcurrentDictionary<int, InterfacedObserver>();

        public Communicator(ILog logger, IPEndPoint remoteEndPoint,
                            Func<Communicator, TcpConnection> connectionFactory)
        {
            _state = StateType.None;
            _logger = logger;
            _remoteEndPoint = remoteEndPoint;
            _connectionFactory = connectionFactory;

#if !NET35
            TaskFactory = new SlimTaskFactory();
#endif
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

        public TRef CreateRef<TRef>(int actorId = 1)
            where TRef : InterfacedActorRef, new()
        {
            var actorRef = new TRef();
            InterfacedActorRefModifier.SetActor(actorRef, new BoundActorRef(actorId));
            InterfacedActorRefModifier.SetRequestWaiter(actorRef, this);
            return actorRef;
        }

        public TObserver CreateObserver<TObserver>(TObserver observer, bool startPending = false)
            where TObserver : IInterfacedObserver
        {
            var proxy = InterfacedObserver.Create(typeof(TObserver));
            var observerId = IssueObserverId();
            proxy.ObserverId = observerId;
            proxy.Channel = new ObserverEventDispatcher(observer, startPending);
            proxy.Disposed = () => { RemoveObserver(observerId); };
            AddObserver(observerId, proxy);
            return (TObserver)(object)proxy;
        }

        private int IssueObserverId()
        {
            return ++_lastObserverId;
        }

        private void AddObserver(int observerId, InterfacedObserver observer)
        {
            _observerChannelMap.TryAdd(observerId, observer);
        }

        private void RemoveObserver(int observerId)
        {
            InterfacedObserver observer;
            _observerChannelMap.TryRemove(observerId, out observer);
        }

        private InterfacedObserver GetObserver(int observerId)
        {
            InterfacedObserver observer;
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
                    var notificationMessage = new NotificationMessage
                    {
                        ObserverId = p.ActorId,
                        NotificationId = p.RequestId,
                        InvokePayload = (IInvokable)p.Message
                    };
                    if (ObserverEventPoster != null)
                        ObserverEventPoster(_ => observer.Channel.Notify(notificationMessage));
                    else
                        observer.Channel.Notify(notificationMessage);
                    break;

                case PacketType.Response:
                    Action<ResponseMessage> handler;
                    if (_requestResponseMap.TryRemove(p.RequestId, out handler))
                    {
                        var actorRefUpdatable = p.Message as IPayloadActorRefUpdatable;
                        if (actorRefUpdatable != null)
                        {
                            actorRefUpdatable.Update(a =>
                            InterfacedActorRefModifier.SetRequestWaiter((InterfacedActorRef)a, this));
                        }

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
                ActorId = ((BoundActorRef)target).Id,
                Message = requestMessage.InvokePayload,
            }, null);
        }

        public Task SendRequestAndWait(IActorRef target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            var tcs = TaskFactory.Create<bool>();
            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((BoundActorRef)target).Id,
                Message = requestMessage.InvokePayload,
            }, r =>
            {
                if (r.Exception != null)
                    tcs.SetException(r.Exception);
                else
                    tcs.SetResult(true);
            });
            return tcs.Task;
        }

        public Task<TResult> SendRequestAndReceive<TResult>(IActorRef target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            var tcs = TaskFactory.Create<TResult>();
            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((BoundActorRef)target).Id,
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
            return tcs.Task;
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
