using System;
using System.Threading;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public interface IThreadPoolStatic
    {
        Boolean QueueUserWorkItem(WaitCallback callBack);
        Boolean QueueUserWorkItem(WaitCallback callBack, Object state);
    }

    public class ThreadPoolStatic : IThreadPoolStatic
    {
        public Boolean QueueUserWorkItem(WaitCallback callBack)
        {
            return ThreadPool.QueueUserWorkItem(callBack);
        }

        public Boolean QueueUserWorkItem(WaitCallback callBack, Object state)
        {
            return ThreadPool.QueueUserWorkItem(callBack, state);
        }
    }
}
