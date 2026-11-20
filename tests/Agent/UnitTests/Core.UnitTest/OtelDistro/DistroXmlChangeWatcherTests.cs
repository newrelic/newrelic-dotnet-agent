// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

// Uses real FileSystemWatchers on a temp directory with a short coalescing delay. Arming runs inline (the scheduler the
// tests pass) so a test's file change always happens after the watcher is armed; the production scheduler (thread pool)
// has its own test.
[TestFixture]
public class DistroXmlChangeWatcherTests
{
    private const int DelayMilliseconds = 20;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private static readonly Func<bool> NoChange = () => false;

    private string _root;
    private DistroRecordingLogger _logger;
    private DistroXmlChangeWatcher _watcher;
    private SemaphoreSlim _changes;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "nr-distro-xml-watch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
        _changes = new SemaphoreSlim(0);
    }

    [TearDown]
    public void TearDown()
    {
        _watcher?.Dispose();
        Log.Initialize(new NoOpLogger());
        _changes.Dispose();
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private DistroXmlChangeWatcher Create(params string[] directories) =>
        _watcher = new DistroXmlChangeWatcher(directories.Length == 0 ? new[] { _root } : directories, DelayMilliseconds, work => work());

    private void StartWith(Action onChange) => Create().Start(onChange, NoChange);

    private void Count() => _changes.Release();

    private bool WaitForInfo(string fragment)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            if (_logger.Messages(Level.Info).Any(m => m.Contains(fragment)))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    [Test]
    public void Start_FileCreated_LogsAgentTextAndInvokesOnChange()
    {
        Create().Start(Count, NoChange);
        var path = Path.Combine(_root, "A.xml");

        File.WriteAllText(path, "<a/>");

        Assert.Multiple(() =>
        {
            Assert.That(_changes.Wait(Wait), Is.True);
            Assert.That(WaitForInfo("Instrumentation change detected: Created - " + path), Is.True);
        });
    }

    [Test]
    public void Start_FileChangedAndDeleted_LogsEach()
    {
        var path = Path.Combine(_root, "A.xml");
        File.WriteAllText(path, "<a/>");
        Create().Start(Count, NoChange);

        File.AppendAllText(path, " ");
        var changed = WaitForInfo("Instrumentation change detected: Changed - " + path);
        File.Delete(path);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(WaitForInfo("Instrumentation change detected: Deleted - " + path), Is.True);
            Assert.That(_changes.Wait(Wait), Is.True);
        });
    }

    [Test]
    public void Start_FileRenamed_LogsOldAndNewPath()
    {
        var oldPath = Path.Combine(_root, "A.xml");
        var newPath = Path.Combine(_root, "B.xml");
        File.WriteAllText(oldPath, "<a/>");
        Create().Start(Count, NoChange);

        File.Move(oldPath, newPath);

        Assert.Multiple(() =>
        {
            Assert.That(WaitForInfo("Instrumentation change detected: Renamed - " + oldPath + " -> " + newPath), Is.True);
            Assert.That(_changes.Wait(Wait), Is.True);
        });
    }

    [Test]
    public void Start_NonXmlFile_NotWatched()
    {
        Create().Start(Count, NoChange);

        File.WriteAllText(Path.Combine(_root, "A.txt"), "text");

        Assert.That(_changes.Wait(TimeSpan.FromMilliseconds(500)), Is.False);
    }

    [Test]
    public void Start_MissingNullAndEmptyDirectories_WatchesTheOthers()
    {
        Create(Path.Combine(_root, "missing"), null, "", _root).Start(Count, NoChange);

        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");

        Assert.That(_changes.Wait(Wait), Is.True);
    }

    [Test]
    public void Start_Twice_KeepsTheFirstCallback()
    {
        var second = 0;
        var watcher = Create();
        watcher.Start(Count, NoChange);
        watcher.Start(() => Interlocked.Increment(ref second), NoChange);

        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");

        Assert.Multiple(() =>
        {
            Assert.That(_changes.Wait(Wait), Is.True);
            Assert.That(Volatile.Read(ref second), Is.EqualTo(0));
        });
    }

    [Test]
    public void Start_AfterDispose_WatchesNothing()
    {
        var watcher = Create();
        watcher.Dispose();

        watcher.Start(Count, NoChange);
        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");

        Assert.That(_changes.Wait(TimeSpan.FromMilliseconds(500)), Is.False);
    }

    [Test]
    public void Dispose_StopsCallbacks()
    {
        var watcher = Create();
        watcher.Start(Count, NoChange);

        watcher.Dispose();
        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");

        Assert.That(_changes.Wait(TimeSpan.FromMilliseconds(500)), Is.False);
    }

    [Test]
    public void Dispose_BeforeStartAndTwice_DoesNotThrow()
    {
        var watcher = Create();

        Assert.DoesNotThrow(() =>
        {
            watcher.Dispose();
            watcher.Dispose();
        });
    }

    [Test]
    public void OnChangeThrows_LogsErrorAndKeepsWatching()
    {
        var calls = 0;
        StartWith(() =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("first");
            }

            _changes.Release();
        });

        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");
        var deadline = DateTime.UtcNow + Wait;
        while (_logger.Messages(Level.Error).Length == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        File.WriteAllText(Path.Combine(_root, "B.xml"), "<b/>");

        Assert.Multiple(() =>
        {
            Assert.That(_changes.Wait(Wait), Is.True, "the worker survived the exception");
            Assert.That(_logger.Messages(Level.Error)[0], Does.Contain("InvalidOperationException: first"));
        });
    }

    [Test]
    public void LoggerThrows_ChangeIsStillSignalled()
    {
        Log.Initialize(Mock.Create<ILogger>(Behavior.Strict));
        Create().Start(Count, NoChange);

        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");
        var created = _changes.Wait(Wait);
        File.Move(Path.Combine(_root, "A.xml"), Path.Combine(_root, "B.xml"));

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(_changes.Wait(Wait), Is.True);
        });
    }

    [Test]
    public void OnChangeAndLoggerThrow_KeepsWatching()
    {
        Log.Initialize(Mock.Create<ILogger>(Behavior.Strict));
        var calls = 0;
        StartWith(() =>
        {
            Interlocked.Increment(ref calls);
            _changes.Release();
            throw new InvalidOperationException("always");
        });

        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");
        var first = _changes.Wait(Wait);
        Thread.Sleep(200);
        File.WriteAllText(Path.Combine(_root, "B.xml"), "<b/>");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(_changes.Wait(Wait), Is.True, "the worker survived the exception and the failing logger");
        });
    }

    [Test]
    public void Start_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Assert.Throws<ArgumentNullException>(() => Create().Start(null, NoChange)).ParamName, Is.EqualTo("onChange"));
            Assert.That(Assert.Throws<ArgumentNullException>(() => Create().Start(Count, null)).ParamName, Is.EqualTo("changedSinceListing"));
        });
    }

    [Test]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Assert.Throws<ArgumentNullException>(() => new DistroXmlChangeWatcher(null, DelayMilliseconds)).ParamName, Is.EqualTo("directories"));
            Assert.That(Assert.Throws<ArgumentNullException>(() => new DistroXmlChangeWatcher(new[] { _root }, DelayMilliseconds, null)).ParamName, Is.EqualTo("schedule"));
        });
    }

    [Test]
    public void Start_ProductionScheduler_ArmsOffTheCallersThreadAndCatchesUp()
    {
        // The production constructor arms on the thread pool. A change made before arming (the catch-up says so) still
        // runs the callback, with no file-system event at all.
        var callerThread = Thread.CurrentThread.ManagedThreadId;
        var checkedOnThread = -1;
        _watcher = new DistroXmlChangeWatcher(new[] { _root }, DelayMilliseconds);

        _watcher.Start(Count, () =>
        {
            checkedOnThread = Thread.CurrentThread.ManagedThreadId;
            return true;
        });

        Assert.Multiple(() =>
        {
            Assert.That(_changes.Wait(Wait), Is.True, "caught up");
            Assert.That(Volatile.Read(ref checkedOnThread), Is.Not.EqualTo(callerThread).And.Not.EqualTo(-1));
        });
    }

    [Test]
    public void Start_CatchUpFalse_NoCallbackUntilAChange()
    {
        Create().Start(Count, NoChange);
        var early = _changes.Wait(TimeSpan.FromMilliseconds(300));

        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");

        Assert.Multiple(() =>
        {
            Assert.That(early, Is.False);
            Assert.That(_changes.Wait(Wait), Is.True);
        });
    }

    [Test]
    public void Start_DisposedBeforeArming_ArmsNothingAndSkipsTheCatchUp()
    {
        Action arm = null;
        var catchUps = 0;
        _watcher = new DistroXmlChangeWatcher(new[] { _root }, DelayMilliseconds, work => arm = work);
        _watcher.Start(Count, () =>
        {
            catchUps++;
            return true;
        });

        _watcher.Dispose();
        arm();
        File.WriteAllText(Path.Combine(_root, "A.xml"), "<a/>");

        Assert.Multiple(() =>
        {
            Assert.That(catchUps, Is.EqualTo(0));
            Assert.That(_changes.Wait(TimeSpan.FromMilliseconds(500)), Is.False);
        });
    }

    [Test]
    public void Start_CatchUpThrows_TreatedAsAChange()
    {
        Create().Start(Count, () => throw new InvalidOperationException("listing"));

        Assert.Multiple(() =>
        {
            Assert.That(_changes.Wait(Wait), Is.True, "a failing check re-runs rather than risk a lost edit");
            Assert.That(_logger.Messages(Level.Error), Is.Empty);
        });
    }

    [Test]
    public void Start_ArmingFails_LogsErrorDoesNotThrow()
    {
        _watcher = new DistroXmlChangeWatcher(new ThrowingDirectories(), DelayMilliseconds, work => work());

        Assert.DoesNotThrow(() => _watcher.Start(Count, NoChange));

        Assert.Multiple(() =>
        {
            Assert.That(_logger.Messages(Level.Error), Has.Length.EqualTo(1));
            Assert.That(_logger.Messages(Level.Error)[0], Does.StartWith("NR instrumentation XML changes will not be applied live: the watcher could not be started").And.Contain("InvalidOperationException: directories"));
        });
    }

    [Test]
    public void Start_ArmingAndLoggerFail_DoesNotThrow()
    {
        Log.Initialize(Mock.Create<ILogger>(Behavior.Strict));
        _watcher = new DistroXmlChangeWatcher(new ThrowingDirectories(), DelayMilliseconds, work => work());

        Assert.DoesNotThrow(() => _watcher.Start(Count, NoChange));
    }

    [Test]
    public void ProductionDelay_IsTheAgentModeDelay()
    {
        Assert.That(DistroXmlChangeWatcher.ProductionDelayMilliseconds, Is.EqualTo(15000));
    }

    // A directory list that fails when the watcher enumerates it while arming.
    private sealed class ThrowingDirectories : IReadOnlyList<string>
    {
        public int Count => 1;

        public string this[int index] => throw new InvalidOperationException("directories");

        public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("directories");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
