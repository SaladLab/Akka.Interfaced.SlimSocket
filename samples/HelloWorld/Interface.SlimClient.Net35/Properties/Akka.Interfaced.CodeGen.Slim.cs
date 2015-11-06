// ------------------------------------------------------------------------------
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
using ProtoBuf;
using TypeAlias;
using System.ComponentModel;

#region HelloWorld.Interface.IHelloWorld

namespace HelloWorld.Interface
{
    [PayloadTableForInterfacedActor(typeof(IHelloWorld))]
    public static class IHelloWorld_PayloadTable
    {
        public static Type[,] GetPayloadTypes()
        {
            return new Type[,]
            {
                {typeof(AddObserver_Invoke), null},
                {typeof(GetHelloCount_Invoke), typeof(GetHelloCount_Return)},
                {typeof(SayHello_Invoke), typeof(SayHello_Return)},
            };
        }

        [ProtoContract, TypeAlias]
        public class AddObserver_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.Int32 observerId;

            public Type GetInterfaceType() { return typeof(IHelloWorld); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        [ProtoContract, TypeAlias]
        public class GetHelloCount_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            public Type GetInterfaceType() { return typeof(IHelloWorld); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        [ProtoContract, TypeAlias]
        public class GetHelloCount_Return : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public System.Int32 v;

            public Type GetInterfaceType() { return typeof(IHelloWorld); }

            public object Value { get { return v; } }
        }

        [ProtoContract, TypeAlias]
        public class SayHello_Invoke : IInterfacedPayload, IAsyncInvokable
        {
            [ProtoMember(1)] public System.String name;

            public Type GetInterfaceType() { return typeof(IHelloWorld); }

            public Task<IValueGetable> InvokeAsync(object target)
            {
                return null;
            }
        }

        [ProtoContract, TypeAlias]
        public class SayHello_Return : IInterfacedPayload, IValueGetable
        {
            [ProtoMember(1)] public System.String v;

            public Type GetInterfaceType() { return typeof(IHelloWorld); }

            public object Value { get { return v; } }
        }
    }

    public interface IHelloWorld_NoReply
    {
        void AddObserver(System.Int32 observerId);
        void GetHelloCount();
        void SayHello(System.String name);
    }

    public class HelloWorldRef : InterfacedActorRef, IHelloWorld, IHelloWorld_NoReply
    {
        public HelloWorldRef(IActorRef actor, IRequestWaiter requestWaiter, TimeSpan? timeout)
            : base(actor, requestWaiter, timeout)
        {
        }

        public IHelloWorld_NoReply WithNoReply()
        {
            return this;
        }

        public HelloWorldRef WithRequestWaiter(IRequestWaiter requestWaiter)
        {
            return new HelloWorldRef(Actor, requestWaiter, Timeout);
        }

        public HelloWorldRef WithTimeout(TimeSpan? timeout)
        {
            return new HelloWorldRef(Actor, RequestWaiter, timeout);
        }

        public Task AddObserver(System.Int32 observerId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IHelloWorld_PayloadTable.AddObserver_Invoke { observerId = observerId }
            };
            return SendRequestAndWait(requestMessage);
        }

        public Task<System.Int32> GetHelloCount()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IHelloWorld_PayloadTable.GetHelloCount_Invoke {  }
            };
            return SendRequestAndReceive<System.Int32>(requestMessage);
        }

        public Task<System.String> SayHello(System.String name)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IHelloWorld_PayloadTable.SayHello_Invoke { name = name }
            };
            return SendRequestAndReceive<System.String>(requestMessage);
        }

        void IHelloWorld_NoReply.AddObserver(System.Int32 observerId)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IHelloWorld_PayloadTable.AddObserver_Invoke { observerId = observerId }
            };
            SendRequest(requestMessage);
        }

        void IHelloWorld_NoReply.GetHelloCount()
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IHelloWorld_PayloadTable.GetHelloCount_Invoke {  }
            };
            SendRequest(requestMessage);
        }

        void IHelloWorld_NoReply.SayHello(System.String name)
        {
            var requestMessage = new RequestMessage
            {
                InvokePayload = new IHelloWorld_PayloadTable.SayHello_Invoke { name = name }
            };
            SendRequest(requestMessage);
        }
    }
}

#endregion

#region HelloWorld.Interface.IHelloWorldEventObserver

namespace HelloWorld.Interface
{
    public static class IHelloWorldEventObserver_PayloadTable
    {
        [ProtoContract, TypeAlias]
        public class SayHello_Invoke : IInvokable
        {
            [ProtoMember(1)] public System.String name;

            public void Invoke(object target)
            {
                ((IHelloWorldEventObserver)target).SayHello(name);
            }
        }
    }
}

#endregion