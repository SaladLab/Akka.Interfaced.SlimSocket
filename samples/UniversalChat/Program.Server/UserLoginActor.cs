using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Interfaced;
using UniversalChat.Interface;
using Common.Logging;
using System.Net;
using Akka.Cluster.Utility;
using Akka.Interfaced.LogFilter;
using Akka.Interfaced.SlimSocket.Server;

namespace UniversalChat.Program.Server
{
    [Log]
    public class UserLoginActor : InterfacedActor<UserLoginActor>, IUserLogin
    {
        private ILog _logger;
        private ClusterNodeContext _clusterContext;
        private IActorRef _clientSession;

        public UserLoginActor(ClusterNodeContext clusterContext, IActorRef clientSession, EndPoint clientRemoteEndPoint)
        {
            _logger = LogManager.GetLogger(string.Format("UserLoginActor({0})", clientRemoteEndPoint));
            _clusterContext = clusterContext;
            _clientSession = clientSession;
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
            for (int i=0; i<10; i++)
            {
                var reply = await _clusterContext.UserDirectory.Ask<DistributedActorDictionaryMessage.AddReply>(
                    new DistributedActorDictionaryMessage.Add(id, user));
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
