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
                new ClusterActorDiscoveryMessage.MonitorActor("User"), Self);

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor("Room"), Self);
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorUp m)
        {
            switch (m.Tag)
            {
                case "User":
                    _clusterContext.UserTable = m.Actor;
                    break;

                case "Room":
                    _clusterContext.RoomTable = m.Actor;
                    break;
            }
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorDown m)
        {
            switch (m.Tag)
            {
                case "User":
                    _clusterContext.UserTable = null;
                break;

                case "Room":
                    _clusterContext.RoomTable = null;
                break;
            }
        }
    }
}
