using System.Threading.Tasks;
using Akka.Interfaced;

namespace HelloWorld.Interface
{
    public interface IHelloWorldEventObserver : IInterfacedObserver
    {
        void SayHello(string name);
    }
}
