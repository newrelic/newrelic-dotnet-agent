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

            Assert.NotNull(objectUnderTest);
            Assert.AreEqual(assemblyName, objectUnderTest.AssemblyName);
            Assert.AreEqual(version, objectUnderTest.Version);
            Assert.NotNull(objectUnderTest.Data);
            Assert.AreEqual(0, objectUnderTest.Data.Count);
        }
    }
}
