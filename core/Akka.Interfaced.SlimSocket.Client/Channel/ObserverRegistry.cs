using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class ObserverRegistry : IObserverRegistry
    {
        public Action<SendOrPostCallback> EventPoster { get; set; }

        private int _lastObserverId;
        private readonly ConcurrentDictionary<int, InterfacedObserver> _observerMap =
            new ConcurrentDictionary<int, InterfacedObserver>();

        public TObserver Create<TObserver>(TObserver observer, bool startPending = false)
            where TObserver : IInterfacedObserver
        {
            var proxy = InterfacedObserver.Create(typeof(TObserver));
            proxy.ObserverId = IssueObserverId();
            proxy.Channel = new ObserverEventDispatcher(observer, startPending);
            AddObserver(proxy.ObserverId, proxy);
            return (TObserver)(object)proxy;
        }

        public void Remove<TObserver>(TObserver observer)
             where TObserver : IInterfacedObserver
        {
            var proxy = (InterfacedObserver)(object)observer;
            RemoveObserver(proxy.ObserverId);
        }

        private int IssueObserverId()
        {
            return ++_lastObserverId;
        }

        private void AddObserver(int observerId, InterfacedObserver observer)
        {
            _observerMap.TryAdd(observerId, observer);
        }

        private void RemoveObserver(int observerId)
        {
            InterfacedObserver observer;
            _observerMap.TryRemove(observerId, out observer);
        }

        private InterfacedObserver GetObserver(int observerId)
        {
            InterfacedObserver observer;
            return _observerMap.TryGetValue(observerId, out observer)
                       ? observer
                       : null;
        }

        public bool OnNotificationPacket(Packet packet)
        {
            var observer = GetObserver(packet.ActorId);
            if (observer == null)
                return false;

            var notificationMessage = new NotificationMessage
            {
                ObserverId = packet.ActorId,
                NotificationId = packet.RequestId,
                InvokePayload = (IInvokable)packet.Message
            };

            if (EventPoster != null)
                EventPoster(_ => observer.Channel.Notify(notificationMessage));
            else
                observer.Channel.Notify(notificationMessage);

            return true;
        }
    }
}
