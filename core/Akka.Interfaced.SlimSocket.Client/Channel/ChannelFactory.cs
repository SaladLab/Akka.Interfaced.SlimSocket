using System;
using System.Threading;
using System.Net;
using Common.Logging;
using Lidgren.Network;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class ChannelFactory
    {
        public ChannelType Type { get; set; }
        public IPEndPoint ConnectEndPoint { get; set; }
        public string ConnectToken { get; set; }
        public Func<ILog> CreateChannelLogger { get; set; }
        public ISlimTaskFactory TaskFactory { get; set; }
        public Action<SendOrPostCallback> ObserverEventPoster { get; set; }
        public Func<IChannel, string, IChannel> ChannelRouter { get; set; }
        public IPacketSerializer PacketSerializer { get; set; }
        public object UdpConfig { get; set; }

        public ChannelFactory()
        {
            TaskFactory = new SlimTaskFactory();
            UdpConfig = new NetPeerConfiguration("SlimSocket");
        }

        public IChannel Create()
        {
            return Create(null);
        }

        public IChannel Create(string address)
        {
            var type = Type;
            var connectEndPoint = ConnectEndPoint;
            var connectToken = ConnectToken;

            if (string.IsNullOrEmpty(address) == false)
            {
                var parts = address.Split('|'); // type|endpoint|token
                if (parts.Length < 3)
                    throw new ArgumentException(nameof(address));
                type = (ChannelType)Enum.Parse(typeof(ChannelType), parts[0], true);
                connectEndPoint = IPEndPointHelper.Parse(parts[1]);
                connectToken = parts[2];
            }

            switch (type)
            {
                case ChannelType.Tcp:
                    var tcpChannel = new TcpChannel(CreateChannelLogger(), connectEndPoint, connectToken, PacketSerializer);
                    InitializeChannel(tcpChannel);
                    return tcpChannel;

                case ChannelType.Udp:
                    var udpChannel = new UdpChannel(CreateChannelLogger(), connectEndPoint, connectToken, PacketSerializer, (NetPeerConfiguration)UdpConfig);
                    InitializeChannel(udpChannel);
                    return udpChannel;

                default:
                    throw new InvalidOperationException("Unknown TransportType");
            }
        }

        private void InitializeChannel(ChannelBase channel)
        {
            channel.TaskFactory = TaskFactory;
            channel.ObserverEventPoster = ObserverEventPoster;
            channel.ChannelRouter = ChannelRouter;
        }
    }
}
