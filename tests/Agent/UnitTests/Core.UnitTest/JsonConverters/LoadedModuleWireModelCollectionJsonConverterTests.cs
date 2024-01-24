// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using NewRelic.Agent.Core.WireModels;
using System.Reflection;
using System;
using System.Text;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class LoadedModuleWireModelCollectionJsonConverterTests
    {
        private const string BaseAssemblyName = "MyTestAssembly";
        private const string BaseAssemblyVersion = "1.0.0";
        private const string BaseAssemblyPath = @"C:\path\to\assembly\MyTestAssembly.dll";
        private const string BaseCompanyName = "MyCompany";
        private const string BaseCopyrightValue = "Copyright 2008";
        private const int BaseHashCode = 42;
        private const string BasePublicKeyToken = "publickeytoken";
        private const string BasePublicKey = "7075626C69636B6579746F6B656E";

        [Test]
        public void LoadedModuleWireModelCollectionIsJsonSerializable()
        {
            var expected = @"[""Jars"",[[""MyTestAssembly"",""1.0.0"",{""namespace"":""MyTestAssembly"",""publicKeyToken"":""7075626C69636B6579746F6B656E"",""assemblyHashCode"":""42"",""Implementation-Vendor"":""MyCompany"",""copyright"":""Copyright 2008""}]]]";

            var baseAssemblyName = new AssemblyName();
            baseAssemblyName.Name = BaseAssemblyName;
            baseAssemblyName.Version = new Version(BaseAssemblyVersion);
            baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

            var baseTestAssembly = new TestAssembly();
            baseTestAssembly.SetAssemblyName = baseAssemblyName;
            baseTestAssembly.SetDynamic = true; // false uses on disk assembly and this won'y have one.
            baseTestAssembly.SetHashCode = BaseHashCode;
            baseTestAssembly.SetLocation = BaseAssemblyPath;
            baseTestAssembly.AddCustomAttribute(new AssemblyCompanyAttribute(BaseCompanyName));
            baseTestAssembly.AddCustomAttribute(new AssemblyCopyrightAttribute(BaseCopyrightValue));

            var assemblies = new List<Assembly>();
            assemblies.Add(baseTestAssembly);
            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            var serialized = JsonConvert.SerializeObject(new[] { loadedModules }, Formatting.None);
            Assert.That(serialized, Is.EqualTo(expected));
        }

        [Test]
        public void LoadedModuleWireModelCollectionHandlesNulls()
        {
            var expected = @"[""Jars"",[[""MyTestAssembly"",""1.0.0"",{""namespace"":""MyTestAssembly"",""publicKeyToken"":""7075626C69636B6579746F6B656E"",""assemblyHashCode"":""42""}]]]";

            var baseAssemblyName = new AssemblyName();
            baseAssemblyName.Name = BaseAssemblyName;
            baseAssemblyName.Version = new Version(BaseAssemblyVersion);
            baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

            var baseTestAssembly = new TestAssembly();
            baseTestAssembly.SetAssemblyName = baseAssemblyName;
            baseTestAssembly.SetDynamic = true; // false uses on disk assembly and this won't have one.
            baseTestAssembly.SetHashCode = BaseHashCode;
            baseTestAssembly.AddCustomAttribute(new AssemblyCompanyAttribute(null));
            baseTestAssembly.AddCustomAttribute(new AssemblyCopyrightAttribute(null));

            var assemblies = new List<Assembly> { baseTestAssembly };

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            var serialized = JsonConvert.SerializeObject(new[] { loadedModules }, Formatting.None);
            Assert.That(serialized, Is.EqualTo(expected));
        }
    }
}
