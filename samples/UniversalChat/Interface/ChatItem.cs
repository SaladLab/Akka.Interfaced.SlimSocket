using System;
using ProtoBuf;

namespace UniversalChat.Interface
{
    [ProtoContract]
    public class ChatItem
    {
        [ProtoMember(1)] public DateTime Time;
        [ProtoMember(2)] public string UserId;
        [ProtoMember(3)] public string Message;
    }
}
