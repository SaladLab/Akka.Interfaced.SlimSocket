﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using UniversalChat.Interface;
using Akka.Cluster.Utility;
using Akka.Interfaced.LogFilter;
using Common.Logging;

namespace UniversalChat.Program.Server
{
    [Log]
    public class RoomDirectoryActor : InterfacedActor<RoomDirectoryActor>, IRoomDirectory
    {
        private ILog _logger = LogManager.GetLogger("RoomDirectory");
        private ClusterNodeContext _clusterContext;
        private List<RoomDirectoryWorkerRef> _workers;
        private int _lastWorkIndex = -1;
        private Dictionary<string, Tuple<RoomDirectoryWorkerRef, IRoom>> _roomTable;

        public RoomDirectoryActor(ClusterNodeContext clusterContext)
        {
            _clusterContext = clusterContext;

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.RegisterActor(Self, nameof(IRoomDirectory)),
                Self);
            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessage.MonitorActor(nameof(IRoomDirectoryWorker)),
                Self);

            _workers = new List<RoomDirectoryWorkerRef>();
            _roomTable = new Dictionary<string, Tuple<RoomDirectoryWorkerRef, IRoom>>();
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorUp message)
        {
            _workers.Add(new RoomDirectoryWorkerRef(message.Actor, this, null));
            _logger.InfoFormat("Registered Actor({0})", message.Actor.Path);
        }

        [MessageHandler]
        private void OnMessage(ClusterActorDiscoveryMessage.ActorDown message)
        {
            _workers.RemoveAll(w => w.Actor.Equals(message.Actor));
            _logger.InfoFormat("Unregistered Actor({0})", message.Actor.Path);
        }

        [MessageHandler]
        private void OnMessage(ShutdownMessage message)
        {
            Context.Stop(Self);
        }

        async Task<IRoom> IRoomDirectory.GetOrCreateRoom(string name)
        {
            Tuple<RoomDirectoryWorkerRef, IRoom> room = null;
            if (_roomTable.TryGetValue(name, out room))
                return room.Item2;

            if (_workers.Count == 0)
                return null;

            // pick a worker for creating RoomActor by round-robin fashion.

            _lastWorkIndex = (_lastWorkIndex + 1) % _workers.Count;
            var worker = _workers[_lastWorkIndex];

            try
            {
                room = Tuple.Create(worker, await worker.CreateRoom(name));
            }
            catch (Exception e)
            {
                // TODO: Write down exception log
                Console.WriteLine(e);
            }

            if (room == null)
                return null;

            _roomTable.Add(name, room);
            return room.Item2;
        }

        Task IRoomDirectory.RemoveRoom(string name)
        {
            Tuple<RoomDirectoryWorkerRef, IRoom> room = null;
            if (_roomTable.TryGetValue(name, out room) == false)
                return Task.FromResult(0);

            _roomTable.Remove(name);
            room.Item1.WithNoReply().RemoveRoom(name);

            return Task.FromResult(true);
        }

        Task<List<string>> IRoomDirectory.GetRoomList()
        {
            return Task.FromResult(_roomTable.Keys.ToList());
        }
    }
}
