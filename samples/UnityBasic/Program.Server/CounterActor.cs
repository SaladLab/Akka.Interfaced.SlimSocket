using System.Threading.Tasks;
using Akka.Interfaced;
using UnityBasic.Interface;

namespace UnityBasic.Program.Server
{
    public class CounterActor : InterfacedActor<CounterActor>, ICounter
    {
        private int _counter = 0;

        Task ICounter.IncCounter(int delta)
        {
            if (delta <= 0)
                throw new CounterException(7);
            _counter += delta;
            return Task.FromResult(true);
        }

        Task<int> ICounter.GetCounter()
        {
            return Task.FromResult(_counter);
        }
    }
}
