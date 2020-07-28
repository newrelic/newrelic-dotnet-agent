using System.Threading;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public interface IThreadPoolStatic
    {
        bool QueueUserWorkItem(WaitCallback callBack);
        bool QueueUserWorkItem(WaitCallback callBack, object state);
    }

    public class ThreadPoolStatic : IThreadPoolStatic
    {
        public bool QueueUserWorkItem(WaitCallback callBack)
        {
            return ThreadPool.QueueUserWorkItem(callBack);
        }

        public bool QueueUserWorkItem(WaitCallback callBack, object state)
        {
            return ThreadPool.QueueUserWorkItem(callBack, state);
        }
    }
}
