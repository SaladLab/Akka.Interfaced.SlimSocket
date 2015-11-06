using System;
using Akka.Interfaced;

namespace UniversalChat.Interface
{
    public interface IUserEventObserver : IInterfacedObserver
    {
        void Whisper(ChatItem chatItem);
        void Invite(string invitorUserId, string roomName);
    }
}
