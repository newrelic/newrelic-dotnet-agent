// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Extensions.Helpers;

public class KafkaClusterIdCache
{
    private static readonly TimeSpan ClusterIdTtl = TimeSpan.FromHours(1);

    private readonly Func<object, string> _resolver;
    private readonly ISimpleSchedulingService _schedulingService;
    private readonly Func<long> _utcTicksProvider;
    private readonly ConcurrentDictionary<string, WeakReference> _clientByKey = new ConcurrentDictionary<string, WeakReference>(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ClusterIdEntry> _clusterIdByKey = new ConcurrentDictionary<string, ClusterIdEntry>(StringComparer.Ordinal);
    private int _tickStarted;

    public KafkaClusterIdCache(Func<object, string> resolver, ISimpleSchedulingService schedulingService, Func<long> utcTicksProvider)
    {
        _resolver = resolver;
        _schedulingService = schedulingService;
        _utcTicksProvider = utcTicksProvider;
    }

    public void Register(string bootstrapServers, object clientInstance)
    {
        if (string.IsNullOrEmpty(bootstrapServers))
            return;

        _clientByKey[bootstrapServers] = new WeakReference(clientInstance);

        if (Interlocked.CompareExchange(ref _tickStarted, 1, 0) == 0)
        {
            _schedulingService.StartExecuteEvery(ResolvePending, TimeSpan.FromSeconds(30), TimeSpan.Zero);
        }
    }

    public void RefreshClientReference(string bootstrapServers, object clientInstance)
    {
        if (string.IsNullOrEmpty(bootstrapServers))
            return;

        if (_clientByKey.TryGetValue(bootstrapServers, out var weakRef) && !weakRef.IsAlive)
        {
            _clientByKey.TryUpdate(bootstrapServers, new WeakReference(clientInstance), weakRef);
        }
    }

    public bool TryGetClusterId(string bootstrapServers, out string clusterId)
    {
        clusterId = null;

        if (string.IsNullOrEmpty(bootstrapServers))
            return false;

        if (_clusterIdByKey.TryGetValue(bootstrapServers, out var entry))
        {
            clusterId = entry.ClusterId;
            return true;
        }

        return false;
    }

    public void ResolvePending()
    {
        foreach (var pair in _clientByKey)
        {
            try
            {
                ResolveKey(pair.Key, pair.Value);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "KafkaClusterIdCache: failed to resolve cluster id for bootstrap servers {0}", pair.Key);
            }
        }
    }

    private void ResolveKey(string key, WeakReference weakRef)
    {
        if (_clusterIdByKey.TryGetValue(key, out var existing) && _utcTicksProvider() - existing.ResolvedAtTicks < ClusterIdTtl.Ticks)
            return;

        // A single Target read decides liveness. IsAlive followed by Target read the same
        // field twice, so the target could be collected between the two reads.
        var client = weakRef.Target;

        if (client == null)
        {
            _clientByKey.TryRemove(key, out _);
            return;
        }

        var resolved = _resolver(client);

        if (string.IsNullOrEmpty(resolved))
            return;

        _clusterIdByKey[key] = new ClusterIdEntry(resolved, _utcTicksProvider());

        Log.Finest("KafkaClusterIdCache: resolved cluster id {0} for bootstrap servers {1}", resolved, key);
    }

    private sealed class ClusterIdEntry
    {
        public string ClusterId { get; }
        public long ResolvedAtTicks { get; }

        public ClusterIdEntry(string clusterId, long resolvedAtTicks)
        {
            ClusterId = clusterId;
            ResolvedAtTicks = resolvedAtTicks;
        }
    }
}
