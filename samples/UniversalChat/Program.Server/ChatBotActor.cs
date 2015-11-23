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

        public class Stop
        {
        }
    }

    public class ChatBotActor : InterfacedActor<ChatBotActor>
    {
        private readonly ILog _log;
        private readonly ClusterNodeContext _clusterContext;
        private string _userId;
        private UserRef _user;
        private OccupantRef _occupant;
        private bool _stopped;

        public ChatBotActor(ClusterNodeContext clusterContext, string name)
        {
            _log = LogManager.GetLogger($"Bot({name})");
            _clusterContext = clusterContext;
        }

        [MessageHandler, Reentrant]
        private async Task Handle(ChatBotMessage.Start m)
        {
            if (_user != null)
                throw new InvalidOperationException("Already started");

            _userId = m.UserId;

            // start login

            var userLoginActor = Context.ActorOf(Props.Create(
                () => new UserLoginActor(_clusterContext, Self, new IPEndPoint(IPAddress.Loopback, 0))));
            var userLogin = new UserLoginRef(userLoginActor, this, null);
            await userLogin.Login(_userId, m.UserId, 1);

            // enter chat

            await _user.EnterRoom(m.RoomName, 2);

            // chat !

            while (_stopped == false)
            {
                await _occupant.Say(DateTime.Now.ToString(), _userId);
                await Task.Delay(1000);
            }

            // outro

            await _user.ExitFromRoom(m.RoomName);

            _user.Actor.Tell(new ActorBoundSessionMessage.SessionTerminated());

            Context.Stop(Self);
        }

        [MessageHandler]
        private void Handle(ChatBotMessage.Stop m)
        {
            _stopped = true;
        }

        [MessageHandler]
        private void Handle(ActorBoundSessionMessage.Bind m)
        {
            if (m.InterfaceType == typeof(IUser))
            {
                _user = new UserRef(m.Actor, this, null);
                Sender.Tell(new ActorBoundSessionMessage.BindReply(0));
                return;
            }

            if (m.InterfaceType == typeof(IOccupant))
            {
                _occupant = new OccupantRef(m.Actor, this, null);
                Sender.Tell(new ActorBoundSessionMessage.BindReply(0));
                return;
            }

            _log.ErrorFormat("Unexpected bind type. (InterfaceType={0}, Actor={1})",
                             m.InterfaceType?.FullName, m.Actor);
        }
    }
}
