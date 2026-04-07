// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using NewRelic.Agent.Api;
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

    private static bool _jobIdWarnLogged;
    private static bool _jobWarnLogged;
    private static bool _queueWarnLogged;
    private static bool _serverIdWarnLogged;
    private static bool _backgroundJobWarnLogged;
    private static bool _jobClassNameWarnLogged;
    private static bool _jobMethodNameWarnLogged;

    /// <summary>
    /// Extracts job ID from BackgroundJob object.
    /// </summary>
    public static string GetJobId(object backgroundJob, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_jobIdWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access Id property on BackgroundJob. Job ID will not be captured.");
                _jobIdWarnLogged = true;
            }

            return null;
        }
    }

    /// <summary>
    /// Extracts Job object from BackgroundJob.
    /// </summary>
    public static object GetJob(object backgroundJob, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_jobWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access Job property on BackgroundJob. Job class and method name will not be captured.");
                _jobWarnLogged = true;
            }

            return null;
        }
    }

    /// <summary>
    /// Extracts queue name from Job object.
    /// </summary>
    public static string GetQueueName(object job, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_queueWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access Queue property on Job. Queue name will not be captured.");
                _queueWarnLogged = true;
            }

            return null;
        }
    }

    /// <summary>
    /// Extracts server ID from PerformContext.
    /// </summary>
    public static string GetServerId(object performContext, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_serverIdWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access ServerId property on PerformContext. Server ID will not be captured.");
                _serverIdWarnLogged = true;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets type name from a Job object.
    /// </summary>
    public static string GetJobClassName(object job, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_jobClassNameWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access Type property on Job. Job class name will not be captured.");
                _jobClassNameWarnLogged = true;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets method name from a Job object.
    /// </summary>
    public static string GetJobMethodName(object job, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_jobMethodNameWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access Method property on Job. Job method name will not be captured.");
                _jobMethodNameWarnLogged = true;
            }

            return null;
        }
    }

    /// <summary>
    /// Gets BackgroundJob from PerformContext.
    /// </summary>
    public static object GetBackgroundJob(object performContext, IAgent agent)
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
        catch (Exception ex)
        {
            if (!_backgroundJobWarnLogged)
            {
                agent.Logger.Warn(ex, "HangfireHelper: Unable to access BackgroundJob property on PerformContext. Job will not be instrumented.");
                _backgroundJobWarnLogged = true;
            }

            return null;
        }
    }
}
