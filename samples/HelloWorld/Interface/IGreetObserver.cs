using Akka.Interfaced;

namespace HelloWorld.Interface
{
    public interface IGreetObserver : IInterfacedObserver
    {
        void Event(string message);
    }
}
