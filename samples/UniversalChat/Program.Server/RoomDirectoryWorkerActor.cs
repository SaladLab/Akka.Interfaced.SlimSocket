using System;
using System.Threading.Tasks;
using Akka.Interfaced;
using UniversalChat.Interface;
using Akka.Actor;
using System.Collections.Generic;
using Common.Logging;
using Akka.Cluster.Utility;
using Akka.Interfaced.LogFilter;

namespace UniversalChat.Program.Server
{
    [Log]
    public class RoomDirectoryWorkerActor : InterfacedActor<RoomDirectoryWorkerActor>, IRoomDirectoryWorker
    {
        private ILog _logger = LogManager.GetLogger("RoomDirectoryWorker");
        private readonly ClusterNodeContext _clusterContext;
        private Dictionary<string, IRoom> _roomTable;
        private int _roomActorCount;
        private bool _isStopped;

        public RoomDirectoryWorkerActor(ClusterNodeContext clusterContext)
        {
            _clusterContext = clusterContext;

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.RegisterActor(Self, nameof(IRoomDirectoryWorker)),
                Self);

            _roomTable = new Dictionary<string, IRoom>();
        }

        [MessageHandler]
        private void OnMessage(ShutdownMessage message)
        {
            if (_isStopped)
                return;

            _logger.Info("Stop");
            _isStopped = true;

            // stop all running client sessions

            if (_roomActorCount > 0)
            {
                Context.ActorSelection("*").Tell(InterfacedPoisonPill.Instance);
            }
            else
            {
                Context.Stop(Self);
            }
        }

        [MessageHandler]
        private void OnMessage(Terminated message)
        {
            _roomActorCount -= 1;
            if (_isStopped && _roomActorCount == 0)
                Context.Stop(Self);
        }

        Task<IRoom> IRoomDirectoryWorker.CreateRoom(string name)
        {
            // create room actor

            IActorRef roomActor = null;
            try
            {
                roomActor = Context.ActorOf(Props.Create<RoomActor>(_clusterContext, name));
                Context.Watch(roomActor);
                _roomActorCount += 1;
            }
            catch (Exception)
            {
                return Task.FromResult((IRoom)null);
            }

            // register it at local directory and return

            var room = new RoomRef(roomActor);
            _roomTable.Add(name, room);
            return Task.FromResult((IRoom)room);
        }

        Task IRoomDirectoryWorker.RemoveRoom(string name)
        {
            IRoom room;
            if (_roomTable.TryGetValue(name, out room) == false)
                return Task.FromResult(0);

            ((RoomRef)room).Actor.Tell(InterfacedPoisonPill.Instance);
            _roomTable.Remove(name);
            return Task.FromResult(0);
        }
    }
}
