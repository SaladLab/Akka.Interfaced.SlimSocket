using System;
using System.Threading;

namespace Akka.Interfaced.SlimSocket.Client
{
    public class SlimTaskCompletionSource<TResult> : Task<TResult>, ISlimTaskCompletionSource<TResult>
    {
        private Exception _exception;
        private TResult _result;
        private ManualResetEvent _event = new ManualResetEvent(false);

        public object WaitHandle
        {
            get { return _event; }
        }

        public TaskStatus Status
        {
            get; private set;
        }

        public Exception Exception
        {
            get { return _exception; }
        }

        public TResult Result
        {
            get
            {
                _event.WaitOne();
                return _result;
            }
        }

        public bool IsCompleted
        {
            get
            {
                return Status == TaskStatus.RanToCompletion ||
                       Status == TaskStatus.Canceled ||
                       Status == TaskStatus.Faulted;
            }
        }

        public bool IsSucceeded
        {
            get { return Status == TaskStatus.RanToCompletion; }
        }

        public bool IsFailed
        {
            get
            {
                return Status == TaskStatus.Canceled ||
                       Status == TaskStatus.Faulted;
            }
        }

        public override string ToString()
        {
            if (Status == TaskStatus.RanToCompletion)
                return "Result: " + Result;
            if (Status == TaskStatus.Faulted)
                return "Faulted: " + Exception;
            if (Status == TaskStatus.Canceled)
                return "Canceled";

            return "Status: " + Status;
        }

        public void SetCanceled()
        {
            Status = TaskStatus.Canceled;
            _exception = new OperationCanceledException();
            _event.Set();
        }

        public void SetException(Exception e)
        {
            Status = TaskStatus.Faulted;
            _exception = e;
            _event.Set();
        }

        public void SetResult(TResult result)
        {
            Status = TaskStatus.RanToCompletion;
            _result = result;
            _event.Set();
        }

        public Task<TResult> Task => this;
    }
}
