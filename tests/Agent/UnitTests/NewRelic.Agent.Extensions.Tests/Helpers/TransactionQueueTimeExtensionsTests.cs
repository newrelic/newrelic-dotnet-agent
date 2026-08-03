// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class TransactionQueueTimeExtensionsTests
{
    private ITransaction _transaction;

    private static readonly Func<Dictionary<string, string>, string, string> GetHeader =
        (headers, name) => headers.TryGetValue(name, out var v) ? v : null;

    [SetUp]
    public void SetUp()
    {
        _transaction = Mock.Create<ITransaction>();
    }

    [Test]
    public void ValidHeader_SetsQueueTime_AndReturnsTrue()
    {
        // A recent X-Request-Start (a few seconds ago) yields a valid, in-cap queue time.
        var startMs = (long)(DateTime.UtcNow.AddSeconds(-3) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={startMs}"
        };

        TimeSpan capturedQueueTime = TimeSpan.MinValue;
        Mock.Arrange(() => _transaction.SetQueueTime(Arg.IsAny<TimeSpan>()))
            .DoInstead((TimeSpan qt) => capturedQueueTime = qt);

        var result = _transaction.TrySetQueueTimeFromHeaders(headers, GetHeader);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(capturedQueueTime.TotalSeconds, Is.EqualTo(3.0).Within(2.0));
        });
        Mock.Assert(() => _transaction.SetQueueTime(Arg.IsAny<TimeSpan>()), Occurs.Once());
    }

    [Test]
    public void NoValidHeader_DoesNotSetQueueTime_AndReturnsFalse()
    {
        var headers = new Dictionary<string, string>();

        var result = _transaction.TrySetQueueTimeFromHeaders(headers, GetHeader);

        Assert.That(result, Is.False);
        Mock.Assert(() => _transaction.SetQueueTime(Arg.IsAny<TimeSpan>()), Occurs.Never());
    }

    [Test]
    public void GetHeaderThrows_IsCaught_AndReturnsFalse()
    {
        // The parser invokes getHeader directly (no internal try/catch), so a throwing
        // delegate exercises the extension's catch block.
        Func<object, string, string> throwingGetHeader = (_, __) => throw new InvalidOperationException("boom");

        var result = _transaction.TrySetQueueTimeFromHeaders(new object(), throwingGetHeader);

        Assert.That(result, Is.False);
        Mock.Assert(() => _transaction.SetQueueTime(Arg.IsAny<TimeSpan>()), Occurs.Never());
    }
}
