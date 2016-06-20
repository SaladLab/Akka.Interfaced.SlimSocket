using System.Collections.Generic;
using Akka.Interfaced;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    public class GreetingActor : InterfacedActor, IGreeterWithObserverSync
    {
        private int _count;
        private List<IGreetObserver> _observers = new List<IGreetObserver>();

        string IGreeterSync.Greet(string name)
        {
            // send a notification 'Event' to all observers
            _observers.ForEach(o => o.Event($"Greet({name})"));
            _count += 1;
            return $"Hello {name}!";
        }

        int IGreeterSync.GetCount()
        {
            return _count;
        }

        void IGreeterWithObserverSync.Subscribe(IGreetObserver observer)
        {
            _observers.Add(observer);
        }

        void IGreeterWithObserverSync.Unsubscribe(IGreetObserver observer)
        {
            _observers.Remove(observer);
        }
    }
}
