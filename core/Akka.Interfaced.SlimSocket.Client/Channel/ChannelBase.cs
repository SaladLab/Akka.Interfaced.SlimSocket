using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace Akka.Interfaced.SlimSocket.Client
{
    public enum ChannelStateType
    {
        Closed,
        Connecting,
        TokenChecking,
        Connected,
    }

    public abstract class ChannelBase : IChannel
    {
        public ChannelStateType State => _state;
        public ISlimTaskFactory TaskFactory { get; set; }
        public IObserverRegistry ObserverRegistry { get; set; }

        public event Action<IChannel, ChannelStateType> StateChanged;
        public Func<IChannel, string, IChannel> ChannelRouter { get; set; }

        protected volatile ChannelStateType _state;
        protected readonly ILog _logger;

        private struct ResponseWaitingItem
        {
            public Action<object, ResponseMessage> ResponseHandler;
            public Action<object> CancelHandler;
            public object TaskCompletionSource;
        }

        private int _lastRequestId = 0;
        private readonly ConcurrentDictionary<int, ResponseWaitingItem> _responseWaitingItems =
            new ConcurrentDictionary<int, ResponseWaitingItem>();

        public ChannelBase(ILog logger)
        {
            _state = ChannelStateType.Closed;
            _logger = logger;
        }

        protected bool SetState(ChannelStateType state)
        {
            if (_state == state)
                return false;

            _state = state;
            StateChanged?.Invoke(this, state);

            if (state == ChannelStateType.Closed)
                MakeAllRequestsGetException(new RequestChannelException());

            return true;
        }

        public abstract Task<bool> ConnectAsync();

        public abstract void Close();

        protected abstract void SendRequestPacket(Packet packet);

        public TRef CreateRef<TRef>(int actorId = 1)
            where TRef : InterfacedActorRef, new()
        {
            var actorRef = new TRef();
            InterfacedActorRefModifier.SetTarget(actorRef, new BoundActorTarget(actorId));
            InterfacedActorRefModifier.SetRequestWaiter(actorRef, this);
            return actorRef;
        }

        public TObserver CreateObserver<TObserver>(TObserver observer, bool startPending = false)
            where TObserver : IInterfacedObserver
        {
            return ObserverRegistry.Create(observer, startPending);
        }

        public void RemoveObserver<TObserver>(TObserver observer)
             where TObserver : IInterfacedObserver
        {
            ObserverRegistry.Remove(observer);
        }

        protected void OnPacket(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.Notification:
                    if (ObserverRegistry.OnNotificationPacket(packet) == false)
                    {
                        _logger?.WarnFormat("Notification didn't find observer. (ObserverId={0}, Message={1})",
                                            packet.ActorId, packet.Message.GetType().Name);
                    }
                    break;

                case PacketType.Response:
                    ResponseWaitingItem waitingItem;
                    if (_responseWaitingItems.TryRemove(packet.RequestId, out waitingItem))
                    {
                        var actorRefUpdatable = packet.Message as IPayloadActorRefUpdatable;
                        if (actorRefUpdatable != null)
                        {
                            actorRefUpdatable.Update(a =>
                            {
                                var actorRef = (InterfacedActorRef)a;
                                var target = (BoundActorTarget)actorRef.Target;
                                if (string.IsNullOrEmpty(target.Address))
                                {
                                    // use this channel
                                    InterfacedActorRefModifier.SetRequestWaiter(actorRef, this);
                                }
                                else
                                {
                                    // routed and use channel with specified address
                                    if (ChannelRouter != null)
                                    {
                                        var channel = ChannelRouter(this, target.Address);
                                        if (channel != null)
                                        {
                                            InterfacedActorRefModifier.SetTarget(actorRef, new BoundActorTarget(target.Id));
                                            InterfacedActorRefModifier.SetRequestWaiter(actorRef, channel);
                                        }
                                    }
                                }
                            });
                        }

                        waitingItem.ResponseHandler(waitingItem.TaskCompletionSource, new ResponseMessage
                        {
                            RequestId = packet.RequestId,
                            ReturnPayload = (IValueGetable)packet.Message,
                            Exception = packet.Exception
                        });
                    }
                    break;
            }
        }

        public void SendRequest(IRequestTarget target, RequestMessage requestMessage)
        {
            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((BoundActorTarget)target).Id,
                Message = requestMessage.InvokePayload,
            });
        }

        public Task SendRequestAndWait(IRequestTarget target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            return SendRequestAndReceive<object>(target, requestMessage, timeout);
        }

        public Task<TReturn> SendRequestAndReceive<TReturn>(IRequestTarget target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            var tcs = TaskFactory.Create<TReturn>();

            // Check connection state

            if (State != ChannelStateType.Connected)
            {
                tcs.TrySetException(new RequestChannelException("Channel is not connected."));
                return tcs.Task;
            }

            // Issue requestId and register it in table

            int requestId;
            while (true)
            {
                requestId = ++_lastRequestId;
                if (requestId <= 0)
                    requestId = _lastRequestId = 1;

                var added = _responseWaitingItems.TryAdd(requestId, new ResponseWaitingItem
                {
                    ResponseHandler = (taskCompletionSource, response) =>
                    {
                        var completionSource = ((ISlimTaskCompletionSource<TReturn>)taskCompletionSource);
                        if (response.Exception != null)
                            completionSource.TrySetException(response.Exception);
                        else
                            completionSource.TrySetResult((TReturn)response.ReturnPayload?.Value);
                    },
                    CancelHandler = (taskCompletionSource) =>
                    {
                        var completionSource = ((ISlimTaskCompletionSource<TReturn>)taskCompletionSource);
                        completionSource.TrySetCanceled();
                    },
                    TaskCompletionSource = tcs
                });

                if (added)
                    break;
            }

            // Set timeout

#if !(NET20 || NET35)
            if (timeout != null && timeout.Value != Timeout.InfiniteTimeSpan && timeout.Value > default(TimeSpan))
            {
                var cancellationSource = new CancellationTokenSource();
                cancellationSource.Token.Register(() =>
                {
                    ResponseWaitingItem waitingItem;
                    if (_responseWaitingItems.TryRemove(requestId, out waitingItem))
                    {
                        waitingItem.CancelHandler(waitingItem.TaskCompletionSource);
                    }
                });
                cancellationSource.CancelAfter(timeout.Value);
            }
#endif

            // Fire request

            SendRequestPacket(new Packet
            {
                Type = PacketType.Request,
                ActorId = ((BoundActorTarget)target).Id,
                RequestId = requestId,
                Message = requestMessage.InvokePayload,
            });
            return tcs.Task;
        }

        private void MakeAllRequestsGetException(Exception exception)
        {
            var items = _responseWaitingItems.ToList();
            _responseWaitingItems.Clear();

            foreach (var item in items)
            {
                item.Value.ResponseHandler(item.Value.TaskCompletionSource, new ResponseMessage
                {
                    RequestId = item.Key,
                    Exception = exception
                });
            }
        }
    }
}
