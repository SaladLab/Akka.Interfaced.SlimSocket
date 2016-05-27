using System;
using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public interface ISlimTaskCompletionSource<TResult>
    {
        void SetCanceled();
        void SetException(Exception e);
        void SetResult(TResult result);

        Task<TResult> Task { get; }
    }
}
