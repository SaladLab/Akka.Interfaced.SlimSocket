using System.Threading.Tasks;
using Akka.Interfaced;

namespace UniversalChat.Interface
{
    public interface IRoom : IInterfacedActor
    {
        Task<RoomInfo> Enter(string userId, IRoomObserver observer);
        Task Exit(string userId);
    }
}
