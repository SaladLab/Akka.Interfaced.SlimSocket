using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Interfaced;
using Common.Logging;

namespace UniversalChat.Program.Server
{
    public class ChatBotCommanderMessage
    {
        public class Start
        {
        }

        public class Stop
        {
        }
    }

    public class ChatBotCommanderActor : InterfacedActor<ChatBotCommanderActor>
    {
        private ILog _logger = LogManager.GetLogger("ChatBotCommander");
        private ClusterNodeContext _clusterContext;
        private HashSet<IActorRef> _botSet = new HashSet<IActorRef>();
        private bool _isStopped;

        public ChatBotCommanderActor(ClusterNodeContext clusterContext)
        {
            _clusterContext = clusterContext;
        }

        [MessageHandler]
        private void Handle(ChatBotCommanderMessage.Start m)
        {
            if (_clusterContext.UserTable == null ||
                _clusterContext.RoomTable == null)
            {
                Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(1), Self, m, Self);
                return;
            }

            var chatBot = Context.ActorOf(Props.Create(() => new ChatBotActor(_clusterContext, "bot1")));
            chatBot.Tell(new ChatBotMessage.Start { UserId = "bot1", RoomName = "#bot" });
            Context.Watch(chatBot);
            _botSet.Add(chatBot);
        }

        [MessageHandler]
        private void Handle(ChatBotCommanderMessage.Stop m)
        {
            if (_isStopped)
                return;

            _logger.Info("Stop");
            _isStopped = true;

            // stop all running client sessions

            if (_botSet.Count > 0)
            {
                Context.ActorSelection("*").Tell(new ChatBotMessage.Stop());
            }
            else
            {
                Context.Stop(Self);
            }
        }

        [MessageHandler]
        private void Handle(Terminated m)
        {
            _botSet.Remove(m.ActorRef);

            if (_isStopped && _botSet.Count == 0)
                Context.Stop(Self);
        }
    }
}
