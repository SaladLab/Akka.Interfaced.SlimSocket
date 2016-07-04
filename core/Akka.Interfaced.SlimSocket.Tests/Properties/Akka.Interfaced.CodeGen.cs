﻿// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Akka.Interfaced CodeGenerator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Interfaced;
using Akka.Actor;
using ProtoBuf;
using TypeAlias;
using System.ComponentModel;

#region Akka.Interfaced.SlimSocket.IEntry

namespace Akka.Interfaced.SlimSocket
{
    [PayloadTable(typeof(IEntry), PayloadTableKind.Request)]
    public static class IEntry_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(Echo_Invoke), typeof(Echo_Return) },
                { typeof(GetGreeter_Invoke), typeof(GetGreeter_Return) },
                { typeof(GetGreeterOnAnotherChannel_Invoke), typeof(GetGreeterOnAnotherChannel_Return) },
            };
        }

        [ProtoContract, TypeAlias]
        public class Echo_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public string message;

            public Type GetInterfaceType()
            {
                return typeof(IEntry);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IEntry)__target).Echo(message);
                return (IValueGetable)(new Echo_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class Echo_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public string v;

            public Type GetInterfaceType()
            {
                return typeof(IEntry);
            }

            public object Value
            {
                get { return v; }
            }
        }

        [ProtoContract, TypeAlias]
        public class GetGreeter_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType()
            {
                return typeof(IEntry);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IEntry)__target).GetGreeter();
                return (IValueGetable)(new GetGreeter_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class GetGreeter_Return
            : IInterfacedPayload, IValueGetable, IPayloadActorRefUpdatable
        {
            [ProtoMember(1)] public Akka.Interfaced.SlimSocket.IGreeterWithObserver v;

            public Type GetInterfaceType()
            {
                return typeof(IEntry);
            }

            public object Value
            {
                get { return v; }
            }

            void IPayloadActorRefUpdatable.Update(Action<object> updater)
            {
                if (v != null)
                {
                    updater(v); 
                }
            }
        }

        [ProtoContract, TypeAlias]
        public class GetGreeterOnAnotherChannel_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType()
            {
                return typeof(IEntry);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IEntry)__target).GetGreeterOnAnotherChannel();
                return (IValueGetable)(new GetGreeterOnAnotherChannel_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class GetGreeterOnAnotherChannel_Return
            : IInterfacedPayload, IValueGetable, IPayloadActorRefUpdatable
        {
            [ProtoMember(1)] public Akka.Interfaced.SlimSocket.IGreeterWithObserver v;

            public Type GetInterfaceType()
            {
                return typeof(IEntry);
            }

            public object Value
            {
                get { return v; }
            }

            void IPayloadActorRefUpdatable.Update(Action<object> updater)
            {
                if (v != null)
                {
                    updater(v); 
                }
            }
        }
    }

    public interface IEntry_NoReply
    {
        void Echo(string message);
        void GetGreeter();
        void GetGreeterOnAnotherChannel();
    }

    public class EntryRef : InterfacedActorRef, IEntry, IEntry_NoReply
    {
        public override Type InterfaceType => typeof(IEntry);

        public EntryRef() : base(null)
        {
        }

        public EntryRef(IRequestTarget target) : base(target)
        {
        }

        public EntryRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IEntry_NoReply WithNoReply()
        {
            return this;
        }

        public EntryRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new EntryRef(Target, requestWaiter, Timeout);
        }

        public EntryRef WithTimeout(TimeSpan? timeout)
        {
            return new EntryRef(Target, RequestWaiter, timeout);
        }

        public Task<string> Echo(string message)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEntry_PayloadTable.Echo_Invoke { message = message }
            };
            return SendRequestAndReceive<string>(requestMessage);
        }

        public Task<Akka.Interfaced.SlimSocket.IGreeterWithObserver> GetGreeter()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEntry_PayloadTable.GetGreeter_Invoke {  }
            };
            return SendRequestAndReceive<Akka.Interfaced.SlimSocket.IGreeterWithObserver>(requestMessage);
        }

        public Task<Akka.Interfaced.SlimSocket.IGreeterWithObserver> GetGreeterOnAnotherChannel()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEntry_PayloadTable.GetGreeterOnAnotherChannel_Invoke {  }
            };
            return SendRequestAndReceive<Akka.Interfaced.SlimSocket.IGreeterWithObserver>(requestMessage);
        }

        void IEntry_NoReply.Echo(string message)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEntry_PayloadTable.Echo_Invoke { message = message }
            };
            SendRequest(requestMessage);
        }

        void IEntry_NoReply.GetGreeter()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEntry_PayloadTable.GetGreeter_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IEntry_NoReply.GetGreeterOnAnotherChannel()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IEntry_PayloadTable.GetGreeterOnAnotherChannel_Invoke {  }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIEntry
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIEntry Convert(IEntry value)
        {
            if (value == null) return null;
            return new SurrogateForIEntry { Target = ((EntryRef)value).Target };
        }

        [ProtoConverter]
        public static IEntry Convert(SurrogateForIEntry value)
        {
            if (value == null) return null;
            return new EntryRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IEntry))]
    public interface IEntrySync : IInterfacedActorSync
    {
        string Echo(string message);
        Akka.Interfaced.SlimSocket.IGreeterWithObserver GetGreeter();
        Akka.Interfaced.SlimSocket.IGreeterWithObserver GetGreeterOnAnotherChannel();
    }
}

#endregion
#region Akka.Interfaced.SlimSocket.IGreeter

namespace Akka.Interfaced.SlimSocket
{
    [PayloadTable(typeof(IGreeter), PayloadTableKind.Request)]
    public static class IGreeter_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(GetCount_Invoke), typeof(GetCount_Return) },
                { typeof(Greet_Invoke), typeof(Greet_Return) },
            };
        }

        [ProtoContract, TypeAlias]
        public class GetCount_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType()
            {
                return typeof(IGreeter);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IGreeter)__target).GetCount();
                return (IValueGetable)(new GetCount_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class GetCount_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public int v;

            public Type GetInterfaceType()
            {
                return typeof(IGreeter);
            }

            public object Value
            {
                get { return v; }
            }
        }

        [ProtoContract, TypeAlias]
        public class Greet_Invoke
            : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public string name;

            public Type GetInterfaceType()
            {
                return typeof(IGreeter);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                var __v = await ((IGreeter)__target).Greet(name);
                return (IValueGetable)(new Greet_Return { v = __v });
            }
        }

        [ProtoContract, TypeAlias]
        public class Greet_Return
            : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public string v;

            public Type GetInterfaceType()
            {
                return typeof(IGreeter);
            }

            public object Value
            {
                get { return v; }
            }
        }
    }

    public interface IGreeter_NoReply
    {
        void GetCount();
        void Greet(string name);
    }

    public class GreeterRef : InterfacedActorRef, IGreeter, IGreeter_NoReply
    {
        public override Type InterfaceType => typeof(IGreeter);

        public GreeterRef() : base(null)
        {
        }

        public GreeterRef(IRequestTarget target) : base(target)
        {
        }

        public GreeterRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IGreeter_NoReply WithNoReply()
        {
            return this;
        }

        public GreeterRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new GreeterRef(Target, requestWaiter, Timeout);
        }

        public GreeterRef WithTimeout(TimeSpan? timeout)
        {
            return new GreeterRef(Target, RequestWaiter, timeout);
        }

        public Task<int> GetCount()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.GetCount_Invoke {  }
            };
            return SendRequestAndReceive<int>(requestMessage);
        }

        public Task<string> Greet(string name)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.Greet_Invoke { name = name }
            };
            return SendRequestAndReceive<string>(requestMessage);
        }

        void IGreeter_NoReply.GetCount()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.GetCount_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IGreeter_NoReply.Greet(string name)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.Greet_Invoke { name = name }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIGreeter
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIGreeter Convert(IGreeter value)
        {
            if (value == null) return null;
            return new SurrogateForIGreeter { Target = ((GreeterRef)value).Target };
        }

        [ProtoConverter]
        public static IGreeter Convert(SurrogateForIGreeter value)
        {
            if (value == null) return null;
            return new GreeterRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IGreeter))]
    public interface IGreeterSync : IInterfacedActorSync
    {
        int GetCount();
        string Greet(string name);
    }
}

#endregion
#region Akka.Interfaced.SlimSocket.IGreeterWithObserver

namespace Akka.Interfaced.SlimSocket
{
    [PayloadTable(typeof(IGreeterWithObserver), PayloadTableKind.Request)]
    public static class IGreeterWithObserver_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,] {
                { typeof(Subscribe_Invoke), null },
                { typeof(Unsubscribe_Invoke), null },
            };
        }

        [ProtoContract, TypeAlias]
        public class Subscribe_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadObserverUpdatable
        {
            [ProtoMember(1)] public Akka.Interfaced.SlimSocket.IGreetObserver observer;

            public Type GetInterfaceType()
            {
                return typeof(IGreeterWithObserver);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IGreeterWithObserver)__target).Subscribe(observer);
                return null;
            }

            void IPayloadObserverUpdatable.Update(Action<IInterfacedObserver> updater)
            {
                if (observer != null)
                {
                    updater(observer);
                }
            }
        }

        [ProtoContract, TypeAlias]
        public class Unsubscribe_Invoke
            : IInterfacedPayload, IAsyncInvokable, IPayloadObserverUpdatable
        {
            [ProtoMember(1)] public Akka.Interfaced.SlimSocket.IGreetObserver observer;

            public Type GetInterfaceType()
            {
                return typeof(IGreeterWithObserver);
            }

            public async Task<IValueGetable> InvokeAsync(object __target)
            {
                await ((IGreeterWithObserver)__target).Unsubscribe(observer);
                return null;
            }

            void IPayloadObserverUpdatable.Update(Action<IInterfacedObserver> updater)
            {
                if (observer != null)
                {
                    updater(observer);
                }
            }
        }
    }

    public interface IGreeterWithObserver_NoReply : IGreeter_NoReply
    {
        void Subscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer);
        void Unsubscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer);
    }

    public class GreeterWithObserverRef : InterfacedActorRef, IGreeterWithObserver, IGreeterWithObserver_NoReply
    {
        public override Type InterfaceType => typeof(IGreeterWithObserver);

        public GreeterWithObserverRef() : base(null)
        {
        }

        public GreeterWithObserverRef(IRequestTarget target) : base(target)
        {
        }

        public GreeterWithObserverRef(IRequestTarget target, IRequestWaiter requestWaiter, TimeSpan? timeout = null) : base(target, requestWaiter, timeout)
        {
        }

        public IGreeterWithObserver_NoReply WithNoReply()
        {
            return this;
        }

        public GreeterWithObserverRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new GreeterWithObserverRef(Target, requestWaiter, Timeout);
        }

        public GreeterWithObserverRef WithTimeout(TimeSpan? timeout)
        {
            return new GreeterWithObserverRef(Target, RequestWaiter, timeout);
        }

        public Task Subscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeterWithObserver_PayloadTable.Subscribe_Invoke { observer = (GreetObserver)observer }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task Unsubscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeterWithObserver_PayloadTable.Unsubscribe_Invoke { observer = (GreetObserver)observer }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task<int> GetCount()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.GetCount_Invoke {  }
            };
            return SendRequestAndReceive<int>(requestMessage);
        }

        public Task<string> Greet(string name)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.Greet_Invoke { name = name }
            };
            return SendRequestAndReceive<string>(requestMessage);
        }

        void IGreeterWithObserver_NoReply.Subscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeterWithObserver_PayloadTable.Subscribe_Invoke { observer = (GreetObserver)observer }
            };
            SendRequest(requestMessage);
        }

        void IGreeterWithObserver_NoReply.Unsubscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeterWithObserver_PayloadTable.Unsubscribe_Invoke { observer = (GreetObserver)observer }
            };
            SendRequest(requestMessage);
        }

        void IGreeter_NoReply.GetCount()
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.GetCount_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IGreeter_NoReply.Greet(string name)
        {
            var requestMessage = new RequestMessage {
                InvokePayload = new IGreeter_PayloadTable.Greet_Invoke { name = name }
            };
            SendRequest(requestMessage);
        }
    }

    [ProtoContract]
    public class SurrogateForIGreeterWithObserver
    {
        [ProtoMember(1)] public IRequestTarget Target;

        [ProtoConverter]
        public static SurrogateForIGreeterWithObserver Convert(IGreeterWithObserver value)
        {
            if (value == null) return null;
            return new SurrogateForIGreeterWithObserver { Target = ((GreeterWithObserverRef)value).Target };
        }

        [ProtoConverter]
        public static IGreeterWithObserver Convert(SurrogateForIGreeterWithObserver value)
        {
            if (value == null) return null;
            return new GreeterWithObserverRef(value.Target);
        }
    }

    [AlternativeInterface(typeof(IGreeterWithObserver))]
    public interface IGreeterWithObserverSync : IGreeterSync
    {
        void Subscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer);
        void Unsubscribe(Akka.Interfaced.SlimSocket.IGreetObserver observer);
    }
}

#endregion
#region Akka.Interfaced.SlimSocket.IGreetObserver

namespace Akka.Interfaced.SlimSocket
{
    [PayloadTable(typeof(IGreetObserver), PayloadTableKind.Notification)]
    public static class IGreetObserver_PayloadTable
    {
        public static Type[] GetPayloadTypes()
        {
            return new Type[] {
                typeof(Event_Invoke),
            };
        }

        [ProtoContract, TypeAlias]
        public class Event_Invoke : IInterfacedPayload, IInvokable
        {
            [ProtoMember(1)] public string message;

            public Type GetInterfaceType()
            {
                return typeof(IGreetObserver);
            }

            public void Invoke(object __target)
            {
                ((IGreetObserver)__target).Event(message);
            }
        }
    }

    public class GreetObserver : InterfacedObserver, IGreetObserver
    {
        public GreetObserver()
            : base(null, 0)
        {
        }

        public GreetObserver(INotificationChannel channel, int observerId = 0)
            : base(channel, observerId)
        {
        }

        public void Event(string message)
        {
            var payload = new IGreetObserver_PayloadTable.Event_Invoke { message = message };
            Notify(payload);
        }
    }

    [ProtoContract]
    public class SurrogateForIGreetObserver
    {
        [ProtoMember(1)] public INotificationChannel Channel;
        [ProtoMember(2)] public int ObserverId;

        [ProtoConverter]
        public static SurrogateForIGreetObserver Convert(IGreetObserver value)
        {
            if (value == null) return null;
            var o = (GreetObserver)value;
            return new SurrogateForIGreetObserver { Channel = o.Channel, ObserverId = o.ObserverId };
        }

        [ProtoConverter]
        public static IGreetObserver Convert(SurrogateForIGreetObserver value)
        {
            if (value == null) return null;
            return new GreetObserver(value.Channel, value.ObserverId);
        }
    }

    [AlternativeInterface(typeof(IGreetObserver))]
    public interface IGreetObserverAsync : IInterfacedObserverSync
    {
        Task Event(string message);
    }
}

#endregion
