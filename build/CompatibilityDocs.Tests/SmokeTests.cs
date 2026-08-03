// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void ToolNamespace_IsReachable()
    {
        Assert.That(typeof(Program).Namespace, Is.EqualTo("CompatibilityDocs"));
    }
}
