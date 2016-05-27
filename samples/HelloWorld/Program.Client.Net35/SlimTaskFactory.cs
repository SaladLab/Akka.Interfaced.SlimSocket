using Akka.Interfaced.SlimSocket.Client;

namespace HelloWorld.Program.Client
{
    public class SlimTaskFactory : ISlimTaskFactory
    {
        public ISlimTaskCompletionSource<TResult> Create<TResult>()
        {
            return new SlimTaskCompletionSource<TResult>();
        }
    }
}
