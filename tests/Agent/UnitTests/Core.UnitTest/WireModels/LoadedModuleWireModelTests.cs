// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class LoadedModuleWireModelTests
    {
        [Test]
        public void ConstructorTest()
        {
            var assemblyName = "My.Assembly";
            var version = "1.0.0";
            var objectUnderTest = new LoadedModuleWireModel(assemblyName, version);

            Assert.That(objectUnderTest, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(objectUnderTest.AssemblyName, Is.EqualTo(assemblyName));
                Assert.That(objectUnderTest.Version, Is.EqualTo(version));
                Assert.That(objectUnderTest.Data, Is.Not.Null);
            });
            Assert.That(objectUnderTest.Data, Is.Empty);
        }
    }
}
