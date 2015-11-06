using System;
using System.Collections;
using System.Net;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Base;
using Akka.Interfaced.SlimSocket.Client;
using ProtoBuf.Meta;
using TypeAlias;
using UnityBasic.Interface;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour
{
    public Text LogText;

    void Start()
    {
        var serializer = new PacketSerializer(
            new PacketSerializerBase.Data(
                new ProtoBufMessageSerializer(new InterfaceProtobufSerializer()),
                new TypeAliasTable()));

        G.Comm = new Communicator(G.Logger, new IPEndPoint(IPAddress.Loopback, 5000),
            _ => new TcpConnection(serializer, LogManager.GetLogger("Connection")));
        G.Comm.Start();

        StartCoroutine(ProcessTest());
    }

    void Update()
    {
        // TODO: Observer
    }

    IEnumerator ProcessTest()
    {
        yield return new WaitForSeconds(1);

        WriteLine("Start ProcessTest");
        WriteLine("");

        yield return StartCoroutine(ProcessTestCounter());
        yield return StartCoroutine(ProcessTestCalculator());
        yield return StartCoroutine(ProcessTestPedantic());
    }

    IEnumerator ProcessTestCounter()
    {
        WriteLine("*** Counter ***");

        var counter = new CounterRef(new SlimActorRef(1), new SlimRequestWaiter(G.Comm, this), null);

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

    IEnumerator ProcessTestCalculator()
    {
        WriteLine("*** Calculator ***");

        var calculator = new CalculatorRef(new SlimActorRef(2), new SlimRequestWaiter(G.Comm, this), null);

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

    IEnumerator ProcessTestPedantic()
    {
        WriteLine("*** Pedantic ***");

        var pedantic = new PedanticRef(new SlimActorRef(3), new SlimRequestWaiter(G.Comm, this), null);

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
