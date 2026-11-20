// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper;

[TestFixture]
public class WrapperTracerLookupTests
{
    private IWrapperService _wrapperService;

    [SetUp]
    public void SetUp()
    {
        _wrapperService = Mock.Create<IWrapperService>();
    }

    [Test]
    public void GetTracer_ForwardsAllArgumentsToBeforeWrappedMethod()
    {
        var type = typeof(string);
        var invocationTarget = new object();
        var arguments = new object[] { 1, 2 };
        AfterWrappedMethodDelegate afterWrappedMethodDelegate = (_, __) => { };

        Mock.Arrange(() => _wrapperService.BeforeWrappedMethod(type, "M", "System.Int32", invocationTarget, arguments, "Factory", "Metric", 7u, 42ul, null))
            .Returns(afterWrappedMethodDelegate);

        var result = WrapperTracerLookup.GetTracer(_wrapperService, "Factory", 7u, "Metric", type, "M", "System.Int32", invocationTarget, arguments, 42ul, null);

        Assert.That(result, Is.InstanceOf<WrapperTracer>());
        Mock.Assert(() => _wrapperService.BeforeWrappedMethod(type, "M", "System.Int32", invocationTarget, arguments, "Factory", "Metric", 7u, 42ul, null), Occurs.Once());
    }

    [Test]
    public void GetTracer_NullDelegate_ReturnsNull()
    {
        var type = typeof(string);
        var invocationTarget = new object();
        var arguments = new object[0];

        Mock.Arrange(() => _wrapperService.BeforeWrappedMethod(Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<uint>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Returns((AfterWrappedMethodDelegate)null);

        var result = WrapperTracerLookup.GetTracer(_wrapperService, "Factory", 7u, "Metric", type, "M", "System.Int32", invocationTarget, arguments, 42ul, null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetTracer_WrapperServiceThrows_ReturnsNull()
    {
        var type = typeof(string);
        var invocationTarget = new object();
        var arguments = new object[0];

        Mock.Arrange(() => _wrapperService.BeforeWrappedMethod(Arg.IsAny<Type>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<object>(), Arg.IsAny<object[]>(), Arg.AnyString, Arg.AnyString, Arg.IsAny<uint>(), Arg.IsAny<ulong>(), Arg.IsAny<Type>()))
            .Throws<InvalidOperationException>();

        var result = WrapperTracerLookup.GetTracer(_wrapperService, "Factory", 7u, "Metric", type, "M", "System.Int32", invocationTarget, arguments, 42ul, null);

        Assert.That(result, Is.Null);
    }
}
