using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public interface IChannel : IRequestWaiter
    {
        ChannelStateType State { get; }

        Task<bool> ConnectAsync();

        void Close();

        TRef CreateRef<TRef>(int actorId = 1)
            where TRef : InterfacedActorRef, new();

        TObserver CreateObserver<TObserver>(TObserver observer, bool startPending = false)
            where TObserver : IInterfacedObserver;

        void RemoveObserver<TObserver>(TObserver observer)
            where TObserver : IInterfacedObserver;
    }
}
