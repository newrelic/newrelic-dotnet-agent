// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Hangfire;

/// <summary>
/// Helper class for reflection-based access to Hangfire objects.
/// Uses VisibilityBypasser to avoid direct Hangfire assembly references (LGPL compliance).
/// </summary>
internal static class HangfireHelper
{
    private static Func<object, string> _jobIdAccessor;
    private static Func<object, object> _jobAccessor;
    private static Func<object, string> _queueAccessor;
    private static Func<object, string> _serverIdAccessor;
    private static Func<object, object> _backgroundJobAccessor;
    private static Func<object, Type> _jobTypeAccessor;
    private static Func<object, MethodInfo> _jobMethodAccessor;

    /// <summary>
    /// Extracts job ID from BackgroundJob object.
    /// </summary>
    public static string GetJobId(object backgroundJob)
    {
        if (backgroundJob == null)
        {
            return null;
        }

        try
        {
            _jobIdAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(backgroundJob.GetType(), "Id");
            return _jobIdAccessor(backgroundJob);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts Job object from BackgroundJob.
    /// </summary>
    public static object GetJob(object backgroundJob)
    {
        if (backgroundJob == null)
        {
            return null;
        }

        try
        {
            _jobAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(backgroundJob.GetType(), "Job");
            return _jobAccessor(backgroundJob);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts queue name from Job object.
    /// </summary>
    public static string GetQueueName(object job)
    {
        if (job == null)
        {
            return null;
        }

        try
        {
            _queueAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(job.GetType(), "Queue");
            return _queueAccessor(job);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts server ID from PerformContext.
    /// </summary>
    public static string GetServerId(object performContext)
    {
        if (performContext == null)
        {
            return null;
        }

        try
        {
            _serverIdAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(performContext.GetType(), "ServerId");
            return _serverIdAccessor(performContext);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets type name from a Job object.
    /// </summary>
    public static string GetJobClassName(object job)
    {
        if (job == null)
        {
            return null;
        }

        try
        {
            _jobTypeAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Type>(job.GetType(), "Type");
            var jobType = _jobTypeAccessor(job);
            return jobType?.FullName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets method name from a Job object.
    /// </summary>
    public static string GetJobMethodName(object job)
    {
        if (job == null)
        {
            return null;
        }

        try
        {
            _jobMethodAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<MethodInfo>(job.GetType(), "Method");
            var method = _jobMethodAccessor(job);
            return method?.Name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets BackgroundJob from PerformContext.
    /// </summary>
    public static object GetBackgroundJob(object performContext)
    {
        if (performContext == null)
        {
            return null;
        }

        try
        {
            _backgroundJobAccessor ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(performContext.GetType(), "BackgroundJob");
            return _backgroundJobAccessor(performContext);
        }
        catch
        {
            return null;
        }
    }
}
