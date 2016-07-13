namespace Akka.Interfaced.SlimSocket.Client
{
    public interface IObserverRegistry
    {
        TObserver Create<TObserver>(TObserver observer, bool startPending = false, bool keepOrder = false)
            where TObserver : IInterfacedObserver;

        void Remove<TObserver>(TObserver observer)
             where TObserver : IInterfacedObserver;

        bool OnNotificationPacket(Packet packet);
    }
}
