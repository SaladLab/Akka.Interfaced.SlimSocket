using Akka.Interfaced;

namespace Akka.Interfaced.SlimSocket
{
    public interface IGreetObserver : IInterfacedObserver
    {
        void Event(string message);
    }
}
