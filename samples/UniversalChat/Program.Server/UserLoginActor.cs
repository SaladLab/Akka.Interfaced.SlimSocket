using System;
using System.Net;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Utility;
using Akka.Interfaced;
using Akka.Interfaced.LogFilter;
using Akka.Interfaced.SlimSocket.Server;
using Common.Logging;
using UniversalChat.Interface;

namespace UniversalChat.Program.Server
{
    [Log]
    public class UserLoginActor : InterfacedActor<UserLoginActor>, IUserLogin
    {
        private readonly ILog _logger;
        private readonly ClusterNodeContext _clusterContext;
        private readonly IActorRef _clientSession;
        private readonly IActorRef _userTableContainer;

        public UserLoginActor(ClusterNodeContext clusterContext,
                              IActorRef clientSession, EndPoint clientRemoteEndPoint,
                              IActorRef userTableContainer)
        {
            _logger = LogManager.GetLogger($"UserLoginActor({clientRemoteEndPoint})");
            _clusterContext = clusterContext;
            _clientSession = clientSession;
            _userTableContainer = userTableContainer;
        }

        [MessageHandler]
        private void OnMessage(ClientSessionMessage.BoundSessionTerminated message)
        {
            Context.Stop(Self);
        }

        async Task<int> IUserLogin.Login(string id, string password, int observerId)
        {
            //Contract.Requires<ArgumentNullException>(id != null);
            //Contract.Requires<ArgumentNullException>(password != null);

            // Check password

            if (await Authenticator.AuthenticateAsync(id, password) == false)
                throw new ResultException(ResultCodeType.LoginFailedIncorrectPassword);

            // Make UserActor

            IActorRef user;
            try
            {
                user = Context.System.ActorOf(
                    Props.Create<UserActor>(_clusterContext, _clientSession, id, observerId),
                    "user_" + id);
            }
            catch (Exception)
            {
                throw new ResultException(ResultCodeType.LoginFailedAlreadyConnected);
            }

            // Register User in UserDirectory

            var userRef = new UserRef(user);
            var registered = false;
            for (int i = 0; i < 10; i++)
            {
                var reply = await _userTableContainer.Ask<DistributedActorTableMessage<string>.AddReply>(
                    new DistributedActorTableMessage<string>.Add(id, user));
                if (reply.Added)
                {
                    registered = true;
                    break;
                }
                await Task.Delay(200);
            }
            if (registered == false)
            {
                user.Tell(PoisonPill.Instance);
                throw new ResultException(ResultCodeType.LoginFailedAlreadyConnected);
            }

            // Bind user actor with client session, which makes client to communicate with this actor.

            var reply2 = await _clientSession.Ask<ClientSessionMessage.BindActorResponse>(
                new ClientSessionMessage.BindActorRequest { Actor = user, InterfaceType = typeof(IUser) });

            return reply2.ActorId;
        }
    }
}
