namespace Akka.Interfaced.SlimSocket.Client
{
    public class SlimTaskFactory : ISlimTaskFactory
    {
        public ISlimTaskCompletionSource<TResult> Create<TResult>() => new SlimTaskCompletionSource<TResult>();
    }
}
