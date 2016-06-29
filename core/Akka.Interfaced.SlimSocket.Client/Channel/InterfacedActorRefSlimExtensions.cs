using System;
using System.Threading.Tasks;

namespace Akka.Interfaced.SlimSocket.Client
{
    public static class InterfacedActorRefSlimExtensions
    {
        public static bool IsChannelConnected(this InterfacedActorRef actorRef)
        {
            var channel = actorRef.RequestWaiter as IChannel;
            return channel.State == ChannelStateType.Connected;
        }

        public static Task<bool> ConnectChannelAsync(this InterfacedActorRef actorRef)
        {
            var channel = actorRef.RequestWaiter as IChannel;
            return channel.ConnectAsync();
        }
    }
}
