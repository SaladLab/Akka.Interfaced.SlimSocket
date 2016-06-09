using System;
using System.Collections;
using System.Net;
using Akka.Interfaced;
using Akka.Interfaced.SlimSocket.Client;
using UnityBasic.Interface;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour, IGreetObserver
{
    public Text LogText;

    void Start()
    {
        var comm = CommunicatorHelper.CreateCommunicator<InterfaceProtobufSerializer>(
            G.Logger, new IPEndPoint(IPAddress.Loopback, 5000));
        comm.Start();
        G.Comm = comm;

        StartCoroutine(ProcessTest());
    }

    IEnumerator ProcessTest()
    {
        var entry = G.Comm.CreateRef<EntryRef>();

        WriteLine("Start ProcessTest");
        WriteLine("");

        var t1 = entry.GetGreeter();
        yield return t1.WaitHandle;
        yield return StartCoroutine(ProcessGreeter(t1.Result));

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

    IEnumerator ProcessGreeter(IGreeterWithObserver greeter)
    {
        WriteLine("*** Greeter ***");

        var observer = G.Comm.CreateObserver<IGreetObserver>(this);
        yield return greeter.Subscribe(observer).WaitHandle;

        var t1 = greeter.Greet("World");
        yield return t1.WaitHandle;
        ShowResult(t1, "Greet(Hello)");

        var t2 = greeter.Greet("Actor");
        yield return t2.WaitHandle;
        ShowResult(t2, "Greet(Actor)");

        var t3 = greeter.GetCount();
        yield return t3.WaitHandle;
        ShowResult(t3, "GetCount()");

        yield return greeter.Unsubscribe(observer).WaitHandle;
        // G.Comm.RemoveObserver(observer);

        WriteLine("");
    }

    void IGreetObserver.Event(string message)
    {
        WriteLine(string.Format("<- {0}", message));
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
