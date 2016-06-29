using System.Collections.Generic;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class Communicator
    {
        public ChannelFactory ChannelFactory { get; }
        public IChannel Channel { get; private set; }
        public IList<IChannel> SubChannels { get; }
        public IObserverRegistry ObserverRegistry { get; }

        public Communicator()
        {
            ChannelFactory = new ChannelFactory()
            {
                CreateObserverRegistry = () => ObserverRegistry,
                ChannelRouter = OnSubChannelCreating
            };
            SubChannels = new List<IChannel>();
            ObserverRegistry = new ObserverRegistry();
        }

        public void CreateChannel()
        {
            Channel = ChannelFactory.Create();
        }

        private IChannel OnSubChannelCreating(IChannel parentChannel, string address)
        {
            var newChannel = ChannelFactory.Create(address);

            newChannel.StateChanged += (channel, state) =>
            {
                if (state == ChannelStateType.Closed)
                    OnSubChannelClosed(channel);
            };

            lock (SubChannels)
            {
                SubChannels.Add(newChannel);
            }

            return newChannel;
        }

        private void OnSubChannelClosed(IChannel channel)
        {
            lock (SubChannels)
            {
                SubChannels.Remove(channel);
            }
        }
    }
}
