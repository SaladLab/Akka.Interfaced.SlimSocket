using System;
using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class SlimTaskCompletionSource<TResult> : TaskCompletionSource<TResult>, ISlimTaskCompletionSource<TResult>
    {
    }
}
