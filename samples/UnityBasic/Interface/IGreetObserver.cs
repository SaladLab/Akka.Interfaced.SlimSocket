using Akka.Interfaced;

namespace UnityBasic.Interface
{
    public interface IGreetObserver : IInterfacedObserver
    {
        void Event(string message);
    }
}
