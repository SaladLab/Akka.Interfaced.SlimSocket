using System.Collections.Generic;

namespace Akka.Interfaced.SlimSocket
{
    public class TestGreetObserver : IGreetObserver
    {
        public List<string> Logs = new List<string>();

        public void Event(string message)
        {
            Logs.Add(message);
        }
    }
}
