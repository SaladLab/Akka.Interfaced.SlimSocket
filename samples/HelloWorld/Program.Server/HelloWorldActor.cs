using System.Threading.Tasks;
using Akka.Interfaced;
using HelloWorld.Interface;

namespace HelloWorld.Program.Server
{
    public class HelloWorldActor : InterfacedActor<HelloWorldActor>, IHelloWorld
    {
        private int _helloCount;

        async Task<string> IHelloWorld.SayHello(string name)
        {
            await Task.Delay(100);
            _helloCount += 1;
            return $"Hello {name}!";
        }

        Task<int> IHelloWorld.GetHelloCount()
        {
            return Task.FromResult(_helloCount);
        }
    }
}
