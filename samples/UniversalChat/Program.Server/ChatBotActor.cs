using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using UniversalChat.Interface;

namespace UniversalChat.Program.Server
{
    public static class ChatBotMessage
    {
        public class Start
        {
            public string UserId;
            public string RoomName;
        }
    }

    public class ChatBotActor : InterfacedActor<ChatBotActor>
    {
        private readonly ILog _log;
        private readonly ClusterNodeContext _clusterContext;
        private string _userId;
        private UserRef _user;
        private OccupantRef _occupant;

        public ChatBotActor(ClusterNodeContext clusterContext, string name)
        {
            _log = LogManager.GetLogger($"Bot({name})");
            _clusterContext = clusterContext;
        }

        [MessageHandler]
        private async Task Handle(ChatBotMessage.Start m)
        {
            if (_user != null)
                throw new InvalidOperationException("Already started");

            _userId = m.UserId;

            // start login

            var userLoginActor = Context.ActorOf(Props.Create(
                () => new UserLoginActor(_clusterContext, Self, new IPEndPoint(IPAddress.Loopback, 0))));
            var userLogin = new UserLoginRef(userLoginActor, this, null);
            await userLogin.Login(_userId, "bot", 1);

            // enter chat

            await _user.EnterRoom("#bot", 2);

            // chat !

            for (int i = 0; i < 10000; i++)
            {
                await _occupant.Say(DateTime.Now.ToString(), _userId);
                await Task.Delay(1000);
            }
        }

        [MessageHandler]
        private void Handle(ActorBoundSessionMessage.Bind m)
        {
            if (m.InterfaceType == typeof(IUser))
            {
                _user = new UserRef(m.Actor);
                Sender.Tell(new ActorBoundSessionMessage.BindReply(0));
                return;
            }

            if (m.InterfaceType == typeof(IOccupant))
            {
                _occupant = new OccupantRef(m.Actor);
                Sender.Tell(new ActorBoundSessionMessage.BindReply(0));
                return;
            }

            _log.ErrorFormat("Unexpected bind type. (InterfaceType={0}, Actor={1})",
                             m.InterfaceType?.FullName, m.Actor);
        }
    }
}
