// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Threading.Tasks;

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

            ClassicAssert.NotNull(objectUnderTest);
            ClassicAssert.AreEqual(assemblyName, objectUnderTest.AssemblyName);
            ClassicAssert.AreEqual(version, objectUnderTest.Version);
            ClassicAssert.NotNull(objectUnderTest.Data);
            ClassicAssert.AreEqual(0, objectUnderTest.Data.Count);
        }
    }
}
