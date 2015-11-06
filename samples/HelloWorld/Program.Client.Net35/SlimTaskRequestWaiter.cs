using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using HelloWorld.Interface;
using ProtoBuf.Meta;
using TypeAlias;

namespace HelloWorld.Program.Client
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
            return tcs;
        }

        Task<T> IRequestWaiter.SendRequestAndReceive<T>(IActorRef target, RequestMessage requestMessage,
                                                        TimeSpan? timeout)
        {
            var tcs = new SlimTaskCompletionSource<T>();
            Communicator.SendRequestAndReceive<T>(tcs, target, requestMessage, timeout);
            return tcs;
        }
    }
}
