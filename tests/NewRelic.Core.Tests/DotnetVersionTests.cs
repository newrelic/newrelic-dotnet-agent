// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Core.Tests
{
    [TestFixture]
    public class DotnetVersionTests
    {
        [TestCase(DotnetCoreVersion.LessThan30, ExpectedResult = true)]
        [TestCase(DotnetCoreVersion.netcoreapp30, ExpectedResult = true)]
        [TestCase(DotnetCoreVersion.netcoreapp31, ExpectedResult = true)]
        [TestCase(DotnetCoreVersion.net5, ExpectedResult = true)]
        [TestCase(DotnetCoreVersion.net6, ExpectedResult = false)]
        [TestCase(DotnetCoreVersion.net7, ExpectedResult = true)]
        [TestCase(DotnetCoreVersion.net8, ExpectedResult = false)]
        [TestCase(DotnetCoreVersion.Other, ExpectedResult = false)]
        public bool IsUnsupportedDotnetCoreVersion(DotnetCoreVersion version)
        {
            return DotnetVersion.IsUnsupportedDotnetCoreVersion(version);
        }

        [TestCase(DotnetFrameworkVersion.LessThan45, ExpectedResult = true)]
        [TestCase(DotnetFrameworkVersion.net45, ExpectedResult = true)]
        [TestCase(DotnetFrameworkVersion.net451, ExpectedResult = true)]
        [TestCase(DotnetFrameworkVersion.net452, ExpectedResult = true)]
        [TestCase(DotnetFrameworkVersion.net46, ExpectedResult = true)]
        [TestCase(DotnetFrameworkVersion.net461, ExpectedResult = true)]
        [TestCase(DotnetFrameworkVersion.net462, ExpectedResult = false)]
        [TestCase(DotnetFrameworkVersion.net47, ExpectedResult = false)]
        [TestCase(DotnetFrameworkVersion.net471, ExpectedResult = false)]
        [TestCase(DotnetFrameworkVersion.net472, ExpectedResult = false)]
        [TestCase(DotnetFrameworkVersion.net48, ExpectedResult = false)]
        [TestCase(DotnetFrameworkVersion.net481, ExpectedResult = false)]
        public bool IsUnsupportedDotnetFrameworkVersion(DotnetFrameworkVersion version)
        {
            return DotnetVersion.IsUnsupportedDotnetFrameworkVersion(version);
        }
    }
}
