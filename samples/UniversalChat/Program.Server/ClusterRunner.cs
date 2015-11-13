using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Utility;
using Akka.Configuration;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using ProtoBuf.Meta;
using TypeAlias;
using UniversalChat.Interface;

namespace UniversalChat.Program.Server
{
    class ShutdownMessage
    {
    }

    public class ClusterRunner
    {
        private Config _commonConfig;
        private List<Tuple<ActorSystem, List<IActorRef>>> _nodes = new List<Tuple<ActorSystem, List<IActorRef>>>();

        public ClusterRunner(Config commonConfig)
        {
            _commonConfig = commonConfig;
        }

        public void LaunchNode(int port, int clientPort, params string[] roles)
        {
            var config = _commonConfig
                .WithFallback("akka.remote.helios.tcp.port = " + port)
                .WithFallback("akka.cluster.roles = " + "[" + string.Join(",", roles) + "]");

            var system = ActorSystem.Create("ChatCluster", config);

            DeadRequestProcessingActor.Install(system);

            var cluster = Cluster.Get(system);
            var context = new ClusterNodeContext { System = system };

            context.ClusterActorDiscovery =
                system.ActorOf(Props.Create(() => new ClusterActorDiscovery(cluster)), "ClusterActorDiscovery");
            context.ClusterNodeContextUpdater =
                system.ActorOf(Props.Create(() => new ClusterNodeContextUpdater(context)), "ClusterNodeContextUpdater");

            var actors = new List<IActorRef>();
            foreach (var role in roles)
            {
                switch (role)
                {
                    case "user-table":
                        actors.AddRange(InitUserTable(context));
                        break;

                    case "user":
                        actors.AddRange(InitUser(context, clientPort));
                        break;

                    case "room-table":
                        actors.AddRange(InitRoomTable(context));
                        break;

                    case "room":
                        actors.AddRange(InitRoom(context));
                        break;

                    case "bot":
                        actors.AddRange(InitBot(context));
                        break;

                    default:
                        throw new InvalidOperationException("Invalid role: " + role);
                }
            }

            _nodes.Add(Tuple.Create(system, actors));
        }

        public void Shutdown()
        {
            _nodes.Reverse();
            foreach (var cluster in _nodes)
            {
                // stop all root-actors in reverse

                var rootActors = cluster.Item2;
                rootActors.Reverse();
                foreach (var actor in rootActors)
                    actor.GracefulStop(TimeSpan.FromSeconds(30), new ShutdownMessage()).Wait();

                // stop system

                cluster.Item1.Shutdown();
            }
        }

        private IActorRef[] InitUserTable(ClusterNodeContext context)
        {
            return new[]
            {
                context.System.ActorOf(
                    Props.Create(() => new DistributedActorTable<string>(
                                           "User", context.ClusterActorDiscovery, null, null)),
                    "UserTable")
            };
        }

        private IActorRef[] InitUser(ClusterNodeContext context, int clientPort)
        {
            var container = context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                                       "User", context.ClusterActorDiscovery, null, null)),
                "UserTableContainer");

            var userSystem = new UserClusterSystem(context, container);
            var gateway = userSystem.Start(clientPort, container);

            return new[] { gateway, container };
        }

        private IActorRef[] InitRoomTable(ClusterNodeContext context)
        {
            return new[]
            {
                context.System.ActorOf(
                    Props.Create(() => new DistributedActorTable<string>(
                                           "Room", context.ClusterActorDiscovery, null, null)),
                    "RoomTable")
            };
        }

        private IActorRef[] InitRoom(ClusterNodeContext context)
        {
            return new[]
            {
                context.System.ActorOf(
                    Props.Create(() => new DistributedActorTableContainer<string>(
                                           "Room", context.ClusterActorDiscovery,
                                           typeof(RoomActorFactory), new object[] { context })),
                    "RoomTableContainer")
            };
        }

        private IActorRef[] InitBot(ClusterNodeContext context)
        {
            var container = context.System.ActorOf(
                Props.Create(() => new DistributedActorTableContainer<string>(
                                       "User", context.ClusterActorDiscovery, null, null)),
                "UserTableContainer");

            var botCommander = context.System.ActorOf(
                Props.Create(() => new ChatBotCommanderActor(context, container)), "ChatBotCommander");
            botCommander.Tell(new ChatBotCommanderMessage.Start());

            return new[] { container, botCommander };
        }
    }

    internal class UserClusterSystem
    {
        private readonly ClusterNodeContext _context;
        private readonly IActorRef _userTableContainer;
        private readonly TcpConnectionSettings _tcpConnectionSettings;

        public UserClusterSystem(ClusterNodeContext context, IActorRef userTableContainer)
        {
            _context = context;
            _userTableContainer = userTableContainer;
            _tcpConnectionSettings = new TcpConnectionSettings
            {
                PacketSerializer = new PacketSerializer(
                    new PacketSerializerBase.Data(
                        new ProtoBufMessageSerializer(TypeModel.Create()),
                        new TypeAliasTable()))
            };
        }

        public IActorRef Start(int port, IActorRef userTableContainer)
        {
            var logger = LogManager.GetLogger("ClientGateway");
            var clientGateway = _context.System.ActorOf(Props.Create(() => new ClientGateway(logger, CreateSession)));
            clientGateway.Tell(new ClientGatewayMessage.Start(new IPEndPoint(IPAddress.Any, port)));
            return clientGateway;
        }

        private IActorRef CreateSession(IActorContext context, Socket socket)
        {
            var logger = LogManager.GetLogger($"Client({socket.RemoteEndPoint})");
            return context.ActorOf(Props.Create(
                () => new ClientSession(logger, socket, _tcpConnectionSettings, CreateInitialActor)));
        }

        private Tuple<IActorRef, Type>[] CreateInitialActor(IActorContext context, Socket socket)
        {
            return new[]
            {
                Tuple.Create(
                    context.ActorOf(Props.Create(
                        () => new UserLoginActor(_context, context.Self, socket.RemoteEndPoint, _userTableContainer))),
                    typeof(IUserLogin))
            };
        }
    };

    public class RoomActorFactory : IActorFactory
    {
        private ClusterNodeContext _clusterContext;

        public void Initialize(object[] args)
        {
            _clusterContext = (ClusterNodeContext)args[0];
        }

        public IActorRef CreateActor(IActorRefFactory actorRefFactory, object id, object[] args)
        {
            return actorRefFactory.ActorOf(Props.Create(() => new RoomActor(_clusterContext, (string)id)));
        }
    }
}
