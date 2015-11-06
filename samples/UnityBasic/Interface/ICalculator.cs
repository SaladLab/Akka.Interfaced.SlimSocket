using System;
using System.Threading.Tasks;
using Akka.Interfaced;

namespace UnityBasic.Interface
{
    public interface ICalculator : IInterfacedActor
    {
        Task<string> Concat(string a, string b);
        Task<int> Sum(int a, int b);
        Task<int> Sum(Tuple<int, int> v);
    }
}
