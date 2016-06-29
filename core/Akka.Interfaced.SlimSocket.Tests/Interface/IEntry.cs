using System.Threading.Tasks;
using Akka.Interfaced;

namespace Akka.Interfaced.SlimSocket
{
    public interface IEntry : IInterfacedActor
    {
        Task<string> Echo(string message);
        Task<IGreeterWithObserver> GetGreeter();
        Task<IGreeterWithObserver> GetGreeterOnAnotherChannel();
    }
}
