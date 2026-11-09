// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper;

[TestFixture]
public class WrapperMapTests
{
    private IDefaultWrapper _defaultWrapper;
    private INoOpWrapper _noOpWrapper;

    [SetUp]
    public void SetUp()
    {
        _defaultWrapper = Mock.Create<IDefaultWrapper>();
        _noOpWrapper = Mock.Create<INoOpWrapper>();
    }

    [Test]
    public void ExtensionWrapperCount_ExcludesBuiltInWrappers()
    {
        var wrappers = new List<IWrapper> { Mock.Create<IWrapper>(), Mock.Create<IWrapper>(), Mock.Create<IWrapper>() };

        var wrapperMap = new WrapperMap(wrappers, _defaultWrapper, _noOpWrapper);

        Assert.That(wrapperMap.ExtensionWrapperCount, Is.EqualTo(3));
    }

    [Test]
    public void ExtensionWrapperCount_NoWrappers_IsZero()
    {
        var wrapperMap = new WrapperMap(new List<IWrapper>(), _defaultWrapper, _noOpWrapper);

        Assert.That(wrapperMap.ExtensionWrapperCount, Is.EqualTo(0));
    }

    [Test]
    public void ExtensionWrapperCount_SkipsNullDefaultAndNoOpWrappersPassedIn()
    {
        var wrappers = new List<IWrapper> { Mock.Create<IWrapper>(), null, Mock.Create<IDefaultWrapper>(), Mock.Create<INoOpWrapper>(), Mock.Create<IWrapper>() };

        var wrapperMap = new WrapperMap(wrappers, _defaultWrapper, _noOpWrapper);

        Assert.That(wrapperMap.ExtensionWrapperCount, Is.EqualTo(2));
    }
}
