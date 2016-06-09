using System.Threading.Tasks;
using Akka.Interfaced;

namespace HelloWorld.Interface
{
    public interface IEntry : IInterfacedActor
    {
        Task<IGreeterWithObserver> GetGreeter();
    }
}
