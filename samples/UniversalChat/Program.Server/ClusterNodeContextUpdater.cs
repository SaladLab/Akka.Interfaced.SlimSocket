using Akka.Cluster.Utility;
using Akka.Interfaced;
using UniversalChat.Interface;

namespace UniversalChat.Program.Server
{
    public class ClusterNodeContextUpdater : InterfacedActor<ClusterNodeContextUpdater>
    {
        private readonly ClusterNodeContext _clusterContext;

        public ClusterNodeContextUpdater(ClusterNodeContext clusterContext)
        {
            _clusterContext = clusterContext;
        }

        protected override void PreStart()
        {
            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor(nameof(IUserDirectory)), Self);

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor(nameof(IRoomDirectory)), Self);
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorUp m)
        {
            switch (m.Tag)
            {
                case nameof(IUserDirectory):
                    _clusterContext.UserDirectory = new UserDirectoryRef(m.Actor);
                    break;

                case nameof(IRoomDirectory):
                    _clusterContext.RoomDirectory = new RoomDirectoryRef(m.Actor);
                    break;
            }
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorDown m)
        {
            switch (m.Tag)
            {
                case nameof(IUserDirectory):
                    _clusterContext.UserDirectory = null;
                    break;

                case nameof(IRoomDirectory):
                    _clusterContext.RoomDirectory = null;
                    break;
            }
        }
    }
}
