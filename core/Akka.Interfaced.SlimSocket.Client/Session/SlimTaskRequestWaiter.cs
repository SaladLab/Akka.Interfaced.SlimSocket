using System;
using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class SlimTaskRequestWaiter : IRequestWaiter
    {
        public Communicator Communicator { get; }

        public SlimTaskRequestWaiter(Communicator communicator)
        {
            Communicator = communicator;
        }

        void IRequestWaiter.SendRequest(IActorRef target, RequestMessage requestMessage)
        {
            Communicator.SendRequest(target, requestMessage);
        }

        Task IRequestWaiter.SendRequestAndWait(IActorRef target, RequestMessage requestMessage, TimeSpan? timeout)
        {
            var tcs = new SlimTaskCompletionSource<bool>();
            Communicator.SendRequestAndWait(tcs, target, requestMessage, timeout);
            return tcs.Task;
        }

        Task<T> IRequestWaiter.SendRequestAndReceive<T>(IActorRef target, RequestMessage requestMessage,
                                                        TimeSpan? timeout)
        {
            var tcs = new SlimTaskCompletionSource<T>();
            Communicator.SendRequestAndReceive<T>(tcs, target, requestMessage, timeout);
            return tcs.Task;
        }
    }
}
