using System;
using System.Threading;
using Common.Logging;
using Common.Logging.Factory;
using Xunit.Abstractions;

namespace Akka.Interfaced.SlimSocket
{
    public class XunitOutputLogger : AbstractLogger
    {
        public class Source
        {
            public ITestOutputHelper Output;
            public object Lock;
            public bool Active;
        }

        private string _name;
        private Source _source;

        public XunitOutputLogger(string name, Source source)
        {
            _name = name;
            _source = source;
        }

        public override bool IsDebugEnabled => true;
        public override bool IsErrorEnabled => true;
        public override bool IsFatalEnabled => true;
        public override bool IsInfoEnabled => true;
        public override bool IsTraceEnabled => true;
        public override bool IsWarnEnabled => true;

        protected override void WriteInternal(LogLevel level, object message, Exception exception)
        {
            if (_source.Active == false)
                return;

            lock (_source.Lock)
            {
                if (_source.Active == false)
                    return;

                if (exception == null)
                {
                    _source.Output.WriteLine($"{_name}: {message}");
                }
                else
                {
                    _source.Output.WriteLine($"{_name}: {message} Exception!={exception}");
                }
            }
        }
    }
}
