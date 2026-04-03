// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Hangfire;

/// <summary>
/// Helper class for reflection-based access to Hangfire objects.
/// Uses VisibilityBypasser to avoid direct Hangfire assembly references (LGPL compliance).
/// </summary>
internal static class HangfireHelper
{
    // Cache reflection accessors for performance
    private static readonly ConcurrentDictionary<Type, Func<object, string>> JobIdAccessors
        = new ConcurrentDictionary<Type, Func<object, string>>();

    private static readonly ConcurrentDictionary<Type, Func<object, object>> JobAccessors
        = new ConcurrentDictionary<Type, Func<object, object>>();

    private static readonly ConcurrentDictionary<Type, Func<object, string>> QueueAccessors
        = new ConcurrentDictionary<Type, Func<object, string>>();

    private static readonly ConcurrentDictionary<Type, Func<object, string>> ServerIdAccessors
        = new ConcurrentDictionary<Type, Func<object, string>>();

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
            var type = backgroundJob.GetType();
            var accessor = JobIdAccessors.GetOrAdd(type, t =>
                VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "Id"));
            return accessor(backgroundJob);
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
            var type = backgroundJob.GetType();
            var accessor = JobAccessors.GetOrAdd(type, t =>
                VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Job"));
            return accessor(backgroundJob);
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
            var type = job.GetType();
            var accessor = QueueAccessors.GetOrAdd(type, t =>
                VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "Queue"));
            return accessor(job);
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
            var type = performContext.GetType();
            var accessor = ServerIdAccessors.GetOrAdd(type, t =>
                VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(t, "ServerId"));
            return accessor(performContext);
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
            // Extract Type property
            var typeProperty = job.GetType().GetProperty("Type",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (typeProperty != null)
            {
                var jobType = typeProperty.GetValue(job) as Type;
                return jobType?.FullName;
            }
        }
        catch
        {
        }

        return null;
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
            // Extract Method property
            var methodProperty = job.GetType().GetProperty("Method",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (methodProperty != null)
            {
                var method = methodProperty.GetValue(job) as System.Reflection.MethodInfo;
                return method?.Name;
            }
        }
        catch
        {
        }

        return null;
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
            var type = performContext.GetType();
            var property = type.GetProperty("BackgroundJob",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (property != null)
                return property.GetValue(performContext);
        }
        catch
        {
        }

        return null;
    }
}
