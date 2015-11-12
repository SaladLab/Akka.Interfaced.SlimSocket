using System;
using Akka.Configuration;
using UniversalChat.Interface;

namespace UniversalChat.Program.Server
{
    class Program
    {
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

            var clusterRunner = new ClusterRunner(commonConfig);

            var standAlone = args.Length > 0 && args[0] == "standalone";
            if (standAlone)
            {
                clusterRunner.LaunchNode(3001, 9001, "room-table", "user-table", "room", "user", "bot");
            }
            else
            {
                clusterRunner.LaunchNode(3001, 0, "room-table");
                clusterRunner.LaunchNode(3002, 0, "user-table");
                clusterRunner.LaunchNode(3011, 0, "room");
                clusterRunner.LaunchNode(3012, 0, "room");
                clusterRunner.LaunchNode(3021, 9001, "user");
                clusterRunner.LaunchNode(3022, 9002, "user");
                clusterRunner.LaunchNode(3031, 0, "bot");
            }

            // wait for stop signal

            Console.WriteLine("Press enter key to exit.");
            Console.ReadLine();

            clusterRunner.Shutdown();
        }
    }
}
