using System;
using System.Collections.Generic;
using System.Linq;
using Akka;
using Akka.Actor;
using Akka.Configuration;
using Akka.Interfaced;
using UniversalChat.Interface;
using System.Net;
using Akka.Cluster.Utility;
using Akka.Interfaced.SlimSocket.Server;
using Akka.Cluster;
using Common.Logging;
using System.Net.Sockets;
using Akka.Interfaced.SlimSocket.Base;
using ProtoBuf.Meta;
using TypeAlias;

namespace UniversalChat.Program.Server
{
    class ShutdownMessage
    {
    }

    class Program
    {
        private static List<Tuple<ActorSystem, List<IActorRef>>> _clusters = new List<Tuple<ActorSystem, List<IActorRef>>>();

        static void Main(string[] args)
        {
            // force interface assembly to be loaded before creating ProtobufSerializer

            var type = typeof(IUser);
            if (type == null)
                throw new InvalidProgramException("!");

            // connect to redis

            try
            {
                RedisStorage.Instance = new RedisStorage("localhost");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in connecting redis server: " + e);
                return;
            }

            // run cluster nodes

            var commonConfig = ConfigurationFactory.ParseString(@"
                akka {
                  actor {
                    provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                    serializers {
                      proto = ""Akka.Interfaced.ProtobufSerializer.ProtobufSerializer, Akka.Interfaced.ProtobufSerializer""
                    }
                    serialization-bindings {
                      ""Akka.Interfaced.NotificationMessage, Akka.Interfaced"" = proto
                      ""Akka.Interfaced.RequestMessage, Akka.Interfaced"" = proto
                      ""Akka.Interfaced.ResponseMessage, Akka.Interfaced"" = proto
                    }
                  }
                  remote {
                    helios.tcp {
                      hostname = ""127.0.0.1""
                    }
                  }
                  cluster {
                    seed-nodes = [""akka.tcp://ChatCluster@127.0.0.1:3001""]
                    auto-down-unreachable-after = 30s
                  }
                }");

            var standAlone = args.Length > 0 && args[0] == "standalone";
            if (standAlone)
            {
                LaunchClusterNode(commonConfig, 3001, 9001, "room-center", "user-center", "room", "user", "bot");
            }
            else
            {
                LaunchClusterNode(commonConfig, 3001, 0, "room-center");
                LaunchClusterNode(commonConfig, 3002, 0, "user-center");
                LaunchClusterNode(commonConfig, 3011, 0, "room");
                LaunchClusterNode(commonConfig, 3012, 0, "room");
                LaunchClusterNode(commonConfig, 3021, 9001, "user");
                LaunchClusterNode(commonConfig, 3022, 9002, "user");
                LaunchClusterNode(commonConfig, 3031, 0, "bot");
            }

            // wait for stop signal

            Console.WriteLine("Press enter key to exit.");
            Console.ReadLine();

            // TODO: Graceful Shutdown

            _clusters.Reverse();
            foreach (var cluster in _clusters)
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

        private static void LaunchClusterNode(Config commonConfig, int port, int clientPort, params string[] roles)
        {
            var config = commonConfig
                .WithFallback("akka.remote.helios.tcp.port = " + port)
                .WithFallback("akka.cluster.roles = " + "[" + string.Join(",", roles) + "]");
            var system = ActorSystem.Create("ChatCluster", config);
            var rootActors = InitClusterNode(system, clientPort, roles);
            _clusters.Add(Tuple.Create(system, rootActors));
        }

        private static List<IActorRef> InitClusterNode(ActorSystem system, int clientPort, params string[] roles)
        {
            DeadRequestProcessingActor.Install(system);

            var cluster = Cluster.Get(system);
            var context = new ClusterNodeContext { System = system };

            context.ClusterActorDiscovery =
                system.ActorOf(Props.Create(() => new ClusterActorDiscovery(cluster)), "ClusterActorDiscovery");
            context.ClusterNodeContextUpdater =
                system.ActorOf(Props.Create(() => new ClusterNodeContextUpdater(context)), "ClusterNodeContextUpdater");

            var rootActors = new List<IActorRef>();
            foreach (var role in roles)
            {
                switch (role)
                {
                    case "user-center":
                        rootActors.Add(system.ActorOf(
                            Props.Create(() => new DistributedActorDictionaryCenter(
                                                   "User", context.ClusterActorDiscovery, null, null)),
                            "UserCenter"));
                        break;

                    case "user":
                        rootActors.Add(system.ActorOf(
                            Props.Create(() => new DistributedActorDictionary(
                                                   "User", context.ClusterActorDiscovery, null, null)),
                            "User"));
                        var userSystem = new UserClusterSystem(context);
                        rootActors.Add(userSystem.Start(clientPort));
                        break;

                    case "room-center":
                        rootActors.Add(system.ActorOf(
                            Props.Create(() => new DistributedActorDictionaryCenter(
                                                   "Room", context.ClusterActorDiscovery, null, null)),
                            "RoomCenter"));
                        break;

                    case "room":
                        rootActors.Add(system.ActorOf(
                            Props.Create(() => new DistributedActorDictionary(
                                                   "Room", context.ClusterActorDiscovery,
                                                   typeof(RoomActorFactory), new object[] { context })),
                            "Room"));
                        break;

                    case "bot":
                        rootActors.Add(system.ActorOf(
                            Props.Create(() => new ChatBotCommanderActor(context)), "chatbot_commander"));
                        rootActors.Last().Tell(new ChatBotCommanderMessage.Start());
                        break;

                    default:
                        throw new InvalidOperationException("Invalid role: " + role);
                }
            }
            return rootActors;
        }

        private class UserClusterSystem
        {
            private ClusterNodeContext _clusterContext;
            private TcpConnectionSettings _tcpConnectionSettings;

            public UserClusterSystem(ClusterNodeContext clusterContext)
            {
                _clusterContext = clusterContext;
            }

            public IActorRef Start(int port)
            {
                var logger = LogManager.GetLogger("ClientGateway");

                _tcpConnectionSettings = new TcpConnectionSettings
                {
                    PacketSerializer = new PacketSerializer(
                        new PacketSerializerBase.Data(
                            new ProtoBufMessageSerializer(TypeModel.Create()),
                            new TypeAliasTable()))
                };

                var clientGateway = _clusterContext.System.ActorOf(Props.Create(() => new ClientGateway(logger, CreateSession)));
                clientGateway.Tell(new ClientGatewayMessage.Start(new IPEndPoint(IPAddress.Any, port)));
                return clientGateway;
            }

            private IActorRef CreateSession(IActorContext context, Socket socket)
            {
                var logger = LogManager.GetLogger($"Client({socket.RemoteEndPoint.ToString()})");
                return context.ActorOf(Props.Create(() => new ClientSession(
                    logger, socket, _tcpConnectionSettings, CreateInitialActor)));
            }

            private Tuple<IActorRef, Type>[] CreateInitialActor(IActorContext context, Socket socket)
            {
                return new[]
                {
                    Tuple.Create(
                        context.ActorOf(Props.Create(
                            () => new UserLoginActor(_clusterContext, context.Self, socket.RemoteEndPoint))),
                        typeof(IUserLogin))
                };
            }
        };

        private class RoomActorFactory : IActorFactory
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
}
