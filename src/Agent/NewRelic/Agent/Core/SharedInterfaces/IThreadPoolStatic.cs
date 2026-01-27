// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;

namespace NewRelic.Agent.Core.SharedInterfaces;

public interface IThreadPoolStatic
{
    bool QueueUserWorkItem(WaitCallback callBack);
    bool QueueUserWorkItem(WaitCallback callBack, object state);

    void GetMaxThreads(out int countMaxWorkerThreads, out int countMaxCompletionThreads);
    void GetMinThreads(out int countMinWorkerThreads, out int countMinCompletionThreads);
    void GetAvailableThreads(out int countAvailWorkerThreads, out int countAvailCompletionThreads);
}

public class ThreadPoolStatic : IThreadPoolStatic
{
    public void GetAvailableThreads(out int countAvailWorkerThreads, out int countAvailCompletionThreads)
    {
        ThreadPool.GetAvailableThreads(out countAvailWorkerThreads, out countAvailCompletionThreads);
    }

    public void GetMaxThreads(out int countMaxWorkerThreads, out int countMaxCompletionThreads)
    {
        ThreadPool.GetMaxThreads(out countMaxWorkerThreads, out countMaxCompletionThreads);
    }

    public void GetMinThreads(out int countMinWorkerThreads, out int countMinCompletionThreads)
    {
        ThreadPool.GetMinThreads(out countMinWorkerThreads, out countMinCompletionThreads);
    }

    public bool QueueUserWorkItem(WaitCallback callBack)
    {
        return ThreadPool.QueueUserWorkItem(callBack);
    }

    public bool QueueUserWorkItem(WaitCallback callBack, object state)
    {
        return ThreadPool.QueueUserWorkItem(callBack, state);
    }
}