using System;
using System.Collections;
using System.Net;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using Common.Logging;
using TypeAlias;
using UnityBasic.Interface;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour, IHelloWorldEventObserver
{
    public Text LogText;

    void Start()
    {
        ApplicationComponent.TryInit();

        var serializer = new PacketSerializer(
            new PacketSerializerBase.Data(
                new ProtoBufMessageSerializer(new InterfaceProtobufSerializer()),
                new TypeAliasTable()));

        var comm = new Communicator(G.Logger, new IPEndPoint(IPAddress.Loopback, 5000),
                                    _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
        comm.TaskFactory = new SlimTaskFactory { Owner = ApplicationComponent.Instance };
        comm.ObserverEventPoster = c => ApplicationComponent.Post(c, null);
        comm.Start();
        G.Comm = comm;

        StartCoroutine(ProcessTest());
    }

    IEnumerator ProcessTest()
    {
        var entry = G.Comm.CreateRef<EntryRef>();

        WriteLine("Start ProcessTest");
        WriteLine("");

        var t1 = entry.GetHelloWorld();
        yield return t1.WaitHandle;
        yield return StartCoroutine(ProcessHelloWorld(t1.Result));

        var t2 = entry.GetCalculator();
        yield return t2.WaitHandle;
        yield return StartCoroutine(ProcessCalculator(t2.Result));

        var t3 = entry.GetCounter();
        yield return t3.WaitHandle;
        yield return StartCoroutine(ProcessCounter(t3.Result));

        var t4 = entry.GetPedantic();
        yield return t4.WaitHandle;
        yield return StartCoroutine(ProcessPedantic(t4.Result));
    }

    // Tests

    IEnumerator ProcessHelloWorld(IHelloWorld helloWorld)
    {
        WriteLine("*** HelloWorld ***");

        // add observer

        var observer = G.Comm.CreateObserver<IHelloWorldEventObserver>(this);
        yield return helloWorld.AddObserver(observer).WaitHandle;

        // make some noise

        var t1 = helloWorld.SayHello("World");
        yield return t1.WaitHandle;
        ShowResult(t1, "SayHello(Hello)");

        var t2 = helloWorld.SayHello("Dlrow");
        yield return t2.WaitHandle;
        ShowResult(t2, "SayHello(Dlrow)");

        var t3 = helloWorld.GetHelloCount();
        yield return t3.WaitHandle;
        ShowResult(t3, "GetHelloCount()");

        WriteLine("");
    }

    void IHelloWorldEventObserver.SayHello(string name)
    {
        WriteLine(string.Format("<- SayHello({0})", name));
    }

    IEnumerator ProcessCalculator(ICalculator calculator)
    {
        WriteLine("*** Calculator ***");

        var t1 = calculator.Sum(1, 2);
        yield return t1.WaitHandle;
        ShowResult(t1, "Sum(1, 2)");

        var t2 = calculator.Sum(Tuple.Create(2, 3));
        yield return t2.WaitHandle;
        ShowResult(t2, "Sum((2, 3))");

        var t3 = calculator.Concat("Hello", "World");
        yield return t3.WaitHandle;
        ShowResult(t3, "Concat(Hello, World)");

        var t4 = calculator.Concat("Hello", null);
        yield return t4.WaitHandle;
        ShowResult(t4, "Concat(Hello, null)");

        WriteLine("");
    }

    IEnumerator ProcessCounter(ICounter counter)
    {
        WriteLine("*** Counter ***");

        yield return counter.IncCounter(1).WaitHandle;
        yield return counter.IncCounter(2).WaitHandle;
        yield return counter.IncCounter(3).WaitHandle;

        var t1 = counter.IncCounter(-1);
        yield return t1.WaitHandle;
        ShowResult(t1, "IncCount(-1)");

        var t2 = counter.GetCounter();
        yield return t2.WaitHandle;
        ShowResult(t2, "GetCounter");

        WriteLine("");
    }

    IEnumerator ProcessPedantic(IPedantic pedantic)
    {
        WriteLine("*** Pedantic ***");

        var t1 = pedantic.TestCall();
        yield return t1.WaitHandle;
        ShowResult(t1, "TestCall");

        var t2 = pedantic.TestOptional(10);
        yield return t2.WaitHandle;
        ShowResult(t2, "TestOptional(10)");

        var t3 = pedantic.TestTuple(Tuple.Create(1, "one"));
        yield return t3.WaitHandle;
        ShowResult(t3, "TestTuple");

        var t4 = pedantic.TestParams(1, 2, 3);
        yield return t4.WaitHandle;
        ShowResult(t4, "TestParams");

        var t5 = pedantic.TestPassClass(new TestParam { Name = "Mouse", Price = 10 });
        yield return t5.WaitHandle;
        ShowResult(t5, "TestPassClass");

        var t6 = pedantic.TestReturnClass(10, 5);
        yield return t6.WaitHandle;
        ShowResult(t6, "TestReturnClass");

        WriteLine("");
    }

    // Utilities

    void WriteLine(string text)
    {
        LogText.text = LogText.text + text + "\n";
    }

    void ShowResult(Task task, string name)
    {
        if (task.Status == TaskStatus.RanToCompletion)
            WriteLine(string.Format("{0}: Done", name));
        else if (task.Status == TaskStatus.Faulted)
            WriteLine(string.Format("{0}: Exception = {1}", name, task.Exception));
        else if (task.Status == TaskStatus.Canceled)
            WriteLine(string.Format("{0}: Canceled", name));
        else
            WriteLine(string.Format("{0}: Illegal Status = {1}", name, task.Status));
    }

    void ShowResult<TResult>(Task<TResult> task, string name)
    {
        if (task.Status == TaskStatus.RanToCompletion)
            WriteLine(string.Format("{0}: Result = {1}", name, task.Result));
        else
            ShowResult((Task)task, name);
    }

}
