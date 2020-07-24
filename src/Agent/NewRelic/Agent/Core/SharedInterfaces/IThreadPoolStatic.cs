using System;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public interface IThreadPoolStatic
    {
        Boolean QueueUserWorkItem([NotNull] WaitCallback callBack);
        Boolean QueueUserWorkItem([NotNull] WaitCallback callBack, Object state);
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
