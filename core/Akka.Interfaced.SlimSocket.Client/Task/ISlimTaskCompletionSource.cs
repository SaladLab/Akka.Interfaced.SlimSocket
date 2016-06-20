using System;
using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public interface ISlimTaskCompletionSource<TResult>
    {
        bool TrySetCanceled();
        bool TrySetException(Exception e);
        bool TrySetResult(TResult result);

        Task<TResult> Task { get; }
    }
}
