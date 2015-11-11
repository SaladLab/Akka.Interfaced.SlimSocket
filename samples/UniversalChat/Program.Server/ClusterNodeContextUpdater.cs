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
                new ClusterActorDiscoveryMessage.MonitorActor("User"), Self);
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorUp m)
        {
            switch (m.Tag)
            {
                case "User":
                    _clusterContext.UserDirectory = m.Actor;
                    break;

                case "Room":
                    _clusterContext.RoomDirectory = m.Actor;
                    break;
            }
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorDown m)
        {
            switch (m.Tag)
            {
                case "User":
                    _clusterContext.UserDirectory = null;
                break;

                case "Room":
                    _clusterContext.RoomDirectory = null;
                break;
            }
        }
    }
}
