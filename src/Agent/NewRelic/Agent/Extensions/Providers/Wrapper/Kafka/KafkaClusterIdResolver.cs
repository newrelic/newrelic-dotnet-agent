// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Confluent.Kafka;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Kafka;

internal static class KafkaClusterIdResolver
{
    private const string ConfluentAssemblyName = "Confluent.Kafka";
    private static readonly TimeSpan DescribeClusterTimeout = TimeSpan.FromSeconds(10);

    private static readonly object LatchLock = new object();
    private static readonly object CacheLock = new object();

    private static readonly ConcurrentDictionary<Type, Func<object, object>> TaskResultAccessors =
        new ConcurrentDictionary<Type, Func<object, object>>();

    private static ReflectionLatchState _latchState = ReflectionLatchState.Unknown;

    private static Func<Handle, object> _dependentBuilderFactory;
    private static Func<object, object> _buildCaller;
    private static object _describeClusterOptions;
    private static Func<object, object, object> _describeClusterCaller;
    private static Func<object, string> _clusterIdAccessor;
    private static Func<object, object> _libHandleAccessor;

    private static KafkaClusterIdCache _cache;

    public static void Register(object clientInstance, string bootstrapServers, IAgent agent)
    {
        EnsureCacheCreated(agent);

        if (_latchState == ReflectionLatchState.Unsupported)
            return;

        _cache.Register(bootstrapServers, clientInstance);
    }

    public static bool TryGetClusterId(string bootstrapServers, out string clusterId)
    {
        clusterId = null;

        if (_cache == null || _latchState == ReflectionLatchState.Unsupported)
            return false;

        return _cache.TryGetClusterId(bootstrapServers, out clusterId);
    }

    public static void RefreshClientReference(string bootstrapServers, object clientInstance)
    {
        if (_cache == null || _latchState == ReflectionLatchState.Unsupported)
            return;

        _cache.RefreshClientReference(bootstrapServers, clientInstance);
    }

    private static void EnsureCacheCreated(IAgent agent)
    {
        if (_cache != null)
            return;

        lock (CacheLock)
        {
            if (_cache != null)
                return;

            var schedulingService = agent.GetExperimentalApi().SimpleSchedulingService;
            _cache = new KafkaClusterIdCache(FetchClusterId, schedulingService, () => DateTime.UtcNow.Ticks);
        }
    }

    private static string FetchClusterId(object clientInstance)
    {
        EnsureDelegatesGenerated();

        if (_latchState == ReflectionLatchState.Unsupported)
            return null;

        try
        {
            if (!(clientInstance is IClient client))
                return null;

            var handle = client.Handle;
            if (handle == null)
                return null;

            var nativeHandle = _libHandleAccessor(handle) as SafeHandle;
            if (nativeHandle == null || nativeHandle.IsClosed || nativeHandle.IsInvalid)
                return null;

            var adminClient = _dependentBuilderFactory(handle);
            if (adminClient == null)
                return null;

            var builtAdminClient = _buildCaller(adminClient);
            if (builtAdminClient == null)
                return null;

            var task = _describeClusterCaller(builtAdminClient, _describeClusterOptions);
            if (task == null)
                return null;

            var taskAsTask = (System.Threading.Tasks.Task)task;
            if (!taskAsTask.Wait(DescribeClusterTimeout))
                return null;

            var taskType = task.GetType();
            var resultAccessor = TaskResultAccessors.GetOrAdd(taskType, GetTaskResultAccessorFunc);
            var describeClusterResult = resultAccessor(task);
            if (describeClusterResult == null)
                return null;

            return _clusterIdAccessor(describeClusterResult);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "KafkaClusterIdResolver: failed to fetch cluster id.");
            return null;
        }
    }

    private static Func<object, object> GetTaskResultAccessorFunc(Type taskType) =>
        VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(taskType, "Result");

    private static void EnsureDelegatesGenerated()
    {
        if (_latchState != ReflectionLatchState.Unknown)
            return;

        lock (LatchLock)
        {
            if (_latchState != ReflectionLatchState.Unknown)
                return;

            try
            {
                _libHandleAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(
                    typeof(Handle), "LibrdkafkaHandle");

                _dependentBuilderFactory = VisibilityBypasser.Instance.GenerateTypeFactory<Handle>(
                    ConfluentAssemblyName, "Confluent.Kafka.DependentAdminClientBuilder");

                _buildCaller = VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<object>(
                    ConfluentAssemblyName, "Confluent.Kafka.DependentAdminClientBuilder", "Build");

                var optionsFactory = VisibilityBypasser.Instance.GenerateTypeFactory(
                    ConfluentAssemblyName, "Confluent.Kafka.Admin.DescribeClusterOptions");
                _describeClusterOptions = optionsFactory();

                _describeClusterCaller = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<object>(
                    ConfluentAssemblyName, "Confluent.Kafka.AdminClient", "DescribeClusterAsync",
                    "Confluent.Kafka.Admin.DescribeClusterOptions");

                _clusterIdAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(
                    ConfluentAssemblyName, "Confluent.Kafka.Admin.DescribeClusterResult", "ClusterId");

                _latchState = ReflectionLatchState.Ready;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "KafkaClusterIdResolver: cluster id reflection is unsupported on this Confluent.Kafka version.");
                _latchState = ReflectionLatchState.Unsupported;
            }
        }
    }

    private enum ReflectionLatchState
    {
        Unknown,
        Ready,
        Unsupported
    }
}
