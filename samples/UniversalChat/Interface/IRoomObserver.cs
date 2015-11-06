using System;
using Akka.Interfaced;

namespace UniversalChat.Interface
{
    public interface IRoomObserver : IInterfacedObserver
    {
        void Enter(string userId);
        void Exit(string userId);
        void Say(ChatItem chatItem);
    }
}
