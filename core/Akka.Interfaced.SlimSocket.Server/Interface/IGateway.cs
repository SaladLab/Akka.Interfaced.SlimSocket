using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Server
{
    public interface IGateway : IInterfacedActor
    {
        Task Start();
        Task Stop(bool stopListenOnly = false);
    }
}
