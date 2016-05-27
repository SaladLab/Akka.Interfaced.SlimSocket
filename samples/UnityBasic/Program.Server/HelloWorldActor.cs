using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Interfaced;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    public class HelloWorldActor : InterfacedActor, IHelloWorld
    {
        private int _helloCount;
        private List<IHelloWorldEventObserver> _observers = new List<IHelloWorldEventObserver>();

        async Task<string> IHelloWorld.SayHello(string name)
        {
            foreach (var observer in _observers)
                observer.SayHello(name);

            await Task.Delay(100);
            _helloCount += 1;
            return $"Hello {name}!";
        }

        Task<int> IHelloWorld.GetHelloCount()
        {
            return Task.FromResult(_helloCount);
        }

        public Task AddObserver(IHelloWorldEventObserver observer)
        {
            _observers.Add(observer);
            return Task.FromResult(0);
        }
    }
}
