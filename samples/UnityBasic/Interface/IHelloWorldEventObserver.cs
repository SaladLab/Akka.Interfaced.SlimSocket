using Akka.Interfaced;

namespace UnityBasic.Interface
{
    public interface IHelloWorldEventObserver : IInterfacedObserver
    {
        void SayHello(string name);
    }
}
