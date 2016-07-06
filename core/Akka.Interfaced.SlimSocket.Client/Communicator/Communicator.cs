using System.Collections.Generic;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class Communicator
    {
        public ChannelFactory ChannelFactory { get; }
        public IList<IChannel> Channels { get; }
        public IObserverRegistry ObserverRegistry { get; }

        public Communicator()
        {
            ChannelFactory = new ChannelFactory()
            {
                CreateObserverRegistry = () => ObserverRegistry,
                ChannelRouter = OnChannelRouting
            };
            Channels = new List<IChannel>();
            ObserverRegistry = new ObserverRegistry();
        }

        public IChannel CreateChannel(string address = null)
        {
            var newChannel = ChannelFactory.Create(address);
            OnChannelCreated(newChannel);
            return newChannel;
        }

        private IChannel OnChannelRouting(IChannel parentChannel, string address)
        {
            return CreateChannel(address);
        }

        private void OnChannelCreated(IChannel newChannel)
        {
            newChannel.StateChanged += (channel, state) =>
            {
                if (state == ChannelStateType.Closed)
                    OnChannelClosed(channel);
            };

            lock (Channels)
            {
                Channels.Add(newChannel);
            }
        }

        private void OnChannelClosed(IChannel channel)
        {
            lock (Channels)
            {
                Channels.Remove(channel);
            }
        }
    }
}
