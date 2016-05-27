namespace Akka.Interfaced.SlimSocket.Client
{
    public interface ISlimTaskFactory
    {
        ISlimTaskCompletionSource<TResult> Create<TResult>();
    }
}
