using System.Threading.Tasks;
using Akka.Interfaced;

namespace HelloWorld.Interface
{
    public interface IHelloWorld : IInterfacedActor
    {
        Task<string> SayHello(string name);
        Task<int> GetHelloCount();
        Task AddObserver(IHelloWorldEventObserver observer);
    }
}
