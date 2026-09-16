// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class KafkaClusterIdCacheTests
{
    private FakeSchedulingService _scheduler;
    private StubResolver _resolver;
    private long _clock;

    [SetUp]
    public void SetUp()
    {
        _scheduler = new FakeSchedulingService();
        _resolver = new StubResolver();
        _clock = 1_000_000L;
    }

    private KafkaClusterIdCache CreateCache()
    {
        return new KafkaClusterIdCache(_resolver.Resolve, _scheduler, () => _clock);
    }

    // 1. Register starts the tick exactly once, no matter how many clients register.
    [Test]
    public void Register_StartsTheTickExactlyOnce_NoMatterHowManyClientsRegister()
    {
        var cache = CreateCache();

        cache.Register("broker1:9092", new object());
        cache.Register("broker2:9092", new object());
        cache.Register("broker1:9092", new object());

        Assert.That(_scheduler.StartExecuteEveryCallCount, Is.EqualTo(1));
        Assert.That(_scheduler.CapturedInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(_scheduler.CapturedInitialDelay, Is.EqualTo(TimeSpan.Zero));
        Assert.That(_scheduler.CapturedAction, Is.Not.Null);
    }

    // Defensive: Register guards the same way TryGetClusterId does (test 9), so a null/empty key is exercised
    // here too rather than left as an untested branch.
    [TestCase(null)]
    [TestCase("")]
    public void Register_IsANoOp_ForANullOrEmptyBootstrapServersKey(string key)
    {
        var cache = CreateCache();

        Assert.DoesNotThrow(() => cache.Register(key, new object()));

        Assert.That(_scheduler.StartExecuteEveryCallCount, Is.EqualTo(0));
    }

    // 2. A tick resolves a pending key, after which TryGetClusterId returns the value.
    [Test]
    public void ResolvePending_ResolvesAPendingKey_SoTryGetClusterIdThenReturnsIt()
    {
        var client = new object();
        var cache = CreateCache();
        _resolver.SetResult(client, "cluster-abc");

        cache.Register("broker1:9092", client);
        cache.ResolvePending();

        var found = cache.TryGetClusterId("broker1:9092", out var clusterId);

        Assert.That(found, Is.True);
        Assert.That(clusterId, Is.EqualTo("cluster-abc"));
    }

    // 3. A resolved key inside the TTL is not re-resolved on later ticks.
    [Test]
    public void ResolvePending_DoesNotReResolve_AResolvedKeyStillInsideTheTtl()
    {
        var client = new object();
        var cache = CreateCache();
        _resolver.SetResult(client, "cluster-abc");

        cache.Register("broker1:9092", client);
        cache.ResolvePending();
        Assert.That(_resolver.CallCount, Is.EqualTo(1));

        _clock += TimeSpan.FromMinutes(30).Ticks;
        cache.ResolvePending();

        Assert.That(_resolver.CallCount, Is.EqualTo(1));
    }

    // 4. A resolve that yields null or empty stores nothing, and the next tick attempts it again.
    [TestCase(null)]
    [TestCase("")]
    public void ResolvePending_StoresNothing_WhenTheResolverYieldsNullOrEmpty_AndRetriesNextTick(string emptyResult)
    {
        var client = new object();
        var cache = CreateCache();
        _resolver.SetResult(client, emptyResult);

        cache.Register("broker1:9092", client);
        cache.ResolvePending();

        Assert.That(cache.TryGetClusterId("broker1:9092", out _), Is.False);
        Assert.That(_resolver.CallCount, Is.EqualTo(1));

        cache.ResolvePending();

        Assert.That(_resolver.CallCount, Is.EqualTo(2));
    }

    // 6. After the TTL elapses the next tick re-resolves, and callers get the stale value until it does.
    [Test]
    public void ResolvePending_ReResolvesAfterTheTtlElapses_ServingTheStaleValueUntilThen()
    {
        var client = new object();
        var cache = CreateCache();
        _resolver.SetResult(client, "cluster-v1");

        cache.Register("broker1:9092", client);
        cache.ResolvePending();

        _clock += TimeSpan.FromMinutes(59).Ticks;
        cache.ResolvePending();
        Assert.That(cache.TryGetClusterId("broker1:9092", out var staleValue), Is.True);
        Assert.That(staleValue, Is.EqualTo("cluster-v1"), "stale value still served just before TTL expiry");
        Assert.That(_resolver.CallCount, Is.EqualTo(1), "not yet re-resolved");

        _resolver.SetResult(client, "cluster-v2");
        _clock += TimeSpan.FromMinutes(2).Ticks;
        cache.ResolvePending();

        Assert.That(cache.TryGetClusterId("broker1:9092", out var freshValue), Is.True);
        Assert.That(freshValue, Is.EqualTo("cluster-v2"));
        Assert.That(_resolver.CallCount, Is.EqualTo(2));
    }

    // 5. A resolver that throws for one key does not stop the tick from resolving the others.
    [Test]
    public void ResolvePending_AResolverThatThrowsForOneKey_DoesNotStopTheTickFromResolvingOthers()
    {
        var badClient = new object();
        var goodClient = new object();
        var cache = CreateCache();
        _resolver.SetThrows(badClient);
        _resolver.SetResult(goodClient, "cluster-good");

        cache.Register("bad:9092", badClient);
        cache.Register("good:9092", goodClient);

        Assert.DoesNotThrow(() => cache.ResolvePending());

        Assert.That(cache.TryGetClusterId("good:9092", out var clusterId), Is.True);
        Assert.That(clusterId, Is.EqualTo("cluster-good"));
        Assert.That(cache.TryGetClusterId("bad:9092", out _), Is.False);
    }

    // 7. A key whose weak reference is dead is dropped, and any resolved value for it is retained.
    [Test]
    public async Task ResolvePending_DropsAKeyWhoseWeakReferenceIsDead_ButKeepsItsResolvedValue()
    {
        var cache = CreateCache();
        RegisterAndResolveAShortLivedClient(cache, "broker1:9092");

        await CollectShortLivedClientsAsync();

        cache.ResolvePending();

        Assert.That(_resolver.CallCount, Is.EqualTo(1), "a dropped key must not be resolved again");
        Assert.That(cache.TryGetClusterId("broker1:9092", out var clusterId), Is.True);
        Assert.That(clusterId, Is.EqualTo("cluster-shortlived"));
    }

    private void RegisterAndResolveAShortLivedClient(KafkaClusterIdCache cache, string key)
    {
        var client = new object();
        _resolver.SetResult(client, "cluster-shortlived");
        cache.Register(key, client);
        cache.ResolvePending();
    }

    // 8. RefreshClientReference replaces a dead weak reference, so the next tick resolves against the live instance.
    [Test]
    public async Task RefreshClientReference_ReplacesADeadWeakReference_SoTheNextTickResolvesTheLiveInstance()
    {
        var cache = CreateCache();
        RegisterAShortLivedClient(cache, "dead:9092");

        await CollectShortLivedClientsAsync();

        var replacement = new object();
        _resolver.SetResult(replacement, "cluster-replacement");

        cache.RefreshClientReference("dead:9092", replacement);
        cache.ResolvePending();

        Assert.That(_resolver.LastCallTarget, Is.SameAs(replacement),
            "a dead weak reference must be replaced with the instance RefreshClientReference was given");
        Assert.That(cache.TryGetClusterId("dead:9092", out var clusterId), Is.True);
        Assert.That(clusterId, Is.EqualTo("cluster-replacement"));
    }

    [Test]
    public void RefreshClientReference_LeavesALiveWeakReferenceUntouched()
    {
        var cache = CreateCache();
        var liveClient = new object();
        cache.Register("live:9092", liveClient);

        cache.RefreshClientReference("live:9092", new object());
        cache.ResolvePending();

        Assert.That(_resolver.LastCallTarget, Is.SameAs(liveClient), "a live weak reference must not be replaced");
    }

    [Test]
    public void RefreshClientReference_IsANoOp_ForAnUnknownKey()
    {
        var cache = CreateCache();

        cache.RefreshClientReference("never-registered:9092", new object());
        cache.ResolvePending();

        Assert.That(_resolver.CallCount, Is.EqualTo(0));
        Assert.That(cache.TryGetClusterId("never-registered:9092", out _), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    public void RefreshClientReference_IsANoOp_ForANullOrEmptyKey(string key)
    {
        var cache = CreateCache();

        Assert.DoesNotThrow(() => cache.RefreshClientReference(key, new object()));

        cache.ResolvePending();

        Assert.That(_resolver.CallCount, Is.EqualTo(0));
    }

    // TryGetClusterId is a pure read: it must not revive a dead weak reference, so the next tick drops the key.
    [Test]
    public async Task TryGetClusterId_DoesNotReplaceADeadWeakReference()
    {
        var cache = CreateCache();
        RegisterAShortLivedClient(cache, "dead:9092");

        await CollectShortLivedClientsAsync();

        cache.TryGetClusterId("dead:9092", out _);
        cache.ResolvePending();

        Assert.That(_resolver.CallCount, Is.EqualTo(0), "a read must not revive a dead key");
    }

    private void RegisterAShortLivedClient(KafkaClusterIdCache cache, string key)
    {
        var client = new object();
        cache.Register(key, client);
    }

    private static async Task CollectShortLivedClientsAsync()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Task.Delay(500);
        GC.Collect();
    }

    // 9. TryGetClusterId returns false for an unresolved key, an unknown key, and a null or empty key, and
    // never calls the resolver.
    [Test]
    public void TryGetClusterId_ReturnsFalse_ForARegisteredButUnresolvedKey_AndNeverCallsTheResolver()
    {
        var cache = CreateCache();
        cache.Register("broker1:9092", new object());

        var found = cache.TryGetClusterId("broker1:9092", out var clusterId);

        Assert.That(found, Is.False);
        Assert.That(clusterId, Is.Null);
        Assert.That(_resolver.CallCount, Is.EqualTo(0));
    }

    [Test]
    public void TryGetClusterId_ReturnsFalse_ForAnUnknownKey_AndNeverCallsTheResolver()
    {
        var cache = CreateCache();

        var found = cache.TryGetClusterId("never-registered:9092", out var clusterId);

        Assert.That(found, Is.False);
        Assert.That(clusterId, Is.Null);
        Assert.That(_resolver.CallCount, Is.EqualTo(0));
    }

    [TestCase(null)]
    [TestCase("")]
    public void TryGetClusterId_ReturnsFalse_ForANullOrEmptyKey_AndNeverCallsTheResolver(string key)
    {
        var cache = CreateCache();

        var found = cache.TryGetClusterId(key, out var clusterId);

        Assert.That(found, Is.False);
        Assert.That(clusterId, Is.Null);
        Assert.That(_resolver.CallCount, Is.EqualTo(0));
    }

    private sealed class FakeSchedulingService : ISimpleSchedulingService
    {
        public int StartExecuteEveryCallCount { get; private set; }
        public Action CapturedAction { get; private set; }
        public TimeSpan CapturedInterval { get; private set; }
        public TimeSpan? CapturedInitialDelay { get; private set; }

        public void StartExecuteEvery(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null)
        {
            StartExecuteEveryCallCount++;
            CapturedAction = action;
            CapturedInterval = timeBetweenExecutions;
            CapturedInitialDelay = optionalInitialDelay;
        }

        public void StopExecuting(Action action)
        {
        }
    }

    private sealed class StubResolver
    {
        private readonly ConditionalWeakTable<object, string> _results = new ConditionalWeakTable<object, string>();
        private readonly ConditionalWeakTable<object, object> _throwFor = new ConditionalWeakTable<object, object>();
        private readonly List<WeakReference> _calls = new List<WeakReference>();

        public int CallCount => _calls.Count;

        public object LastCallTarget => _calls.Count > 0 ? _calls[_calls.Count - 1].Target : null;

        public void SetResult(object client, string clusterId)
        {
            _results.Remove(client);
            _results.Add(client, clusterId);
        }

        public void SetThrows(object client)
        {
            _throwFor.Remove(client);
            _throwFor.Add(client, client);
        }

        public string Resolve(object client)
        {
            _calls.Add(new WeakReference(client));

            if (_throwFor.TryGetValue(client, out _))
                throw new InvalidOperationException("resolver failure");

            return _results.TryGetValue(client, out var value) ? value : null;
        }
    }
}
