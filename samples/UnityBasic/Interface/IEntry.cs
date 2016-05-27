using System.Threading.Tasks;
using Akka.Interfaced;

namespace UnityBasic.Interface
{
    public interface IEntry : IInterfacedActor
    {
        Task<IHelloWorld> GetHelloWorld();
        Task<ICalculator> GetCalculator();
        Task<ICounter> GetCounter();
        Task<IPedantic> GetPedantic();
    }
}
