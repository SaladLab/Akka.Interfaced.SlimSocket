using System.Threading.Tasks;
using Akka.Interfaced;

namespace UnityBasic.Interface
{
    public interface IGreeter : IInterfacedActor
    {
        Task<string> Greet(string name);
        Task<int> GetCount();
    }

    public interface IGreeterWithObserver : IGreeter
    {
        Task Subscribe(IGreetObserver observer);
        Task Unsubscribe(IGreetObserver observer);
    }
}
