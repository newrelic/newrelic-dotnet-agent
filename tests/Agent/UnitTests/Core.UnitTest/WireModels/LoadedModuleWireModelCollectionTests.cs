// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Collections;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class LoadedModuleWireModelCollectionTests
    {
        private const string BaseAssemblyName = "MyTestAssembly";
        private const string BaseAssemblyVersion = "1.0.0";
        private const string BaseAssemblyPath = @"C:\path\to\assembly\MyTestAssembly.dll";
        private const string BaseCompanyName = "MyCompany";
        private const string BaseCopyrightValue = "Copyright 2008";
        private const int BaseHashCode = 42;
        private const string BasePublicKeyToken = "publickeytoken";
        private const string BasePublicKey = "7075626C69636B6579746F6B656E";

        private AssemblyName _baseAssemblyName;
        private TestAssembly _baseTestAssembly;

        [SetUp]
        public void SetUp()
        {
            _baseAssemblyName = new AssemblyName();
            _baseAssemblyName.Name = BaseAssemblyName;
            _baseAssemblyName.Version = new Version(BaseAssemblyVersion);
            _baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

            _baseTestAssembly= new TestAssembly();
            _baseTestAssembly.SetAssemblyName = _baseAssemblyName;
            _baseTestAssembly.SetDynamic = true; // false uses on disk assembly and this won'y have one.
            _baseTestAssembly.SetHashCode = BaseHashCode;
            _baseTestAssembly.SetLocation = BaseAssemblyPath;
            _baseTestAssembly.AddCustomAttribute(new AssemblyCompanyAttribute(BaseCompanyName));
            _baseTestAssembly.AddCustomAttribute(new AssemblyCopyrightAttribute(BaseCopyrightValue));
        }

        [TearDown] public void TearDown()
        {
            _baseAssemblyName = null;
            _baseTestAssembly= null;
        }

        [TestCase(BaseAssemblyName, true, ExpectedResult = 1)]
        [TestCase(BaseAssemblyName, false, ExpectedResult = 1)]
        [TestCase(null, true, ExpectedResult = 0)]
        [TestCase(null, false, ExpectedResult = 0)]
        public int TryGetAssemblyName_UsingCollectionCount(string assemblyName, bool isDynamic)
        {
            _baseAssemblyName.Name = assemblyName;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                _baseTestAssembly.SetLocation = null;
            }

            _baseTestAssembly.SetAssemblyName = _baseAssemblyName;
            _baseTestAssembly.SetDynamic = isDynamic;
            
            var assemblies = new List<Assembly>();
            assemblies.Add(_baseTestAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            return loadedModules.LoadedModules.Count;
        }

        [Test]
        public void ValidateAllData()
        {
            var assemblies = new List<Assembly>();
            assemblies.Add(_baseTestAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(1, loadedModules.LoadedModules.Count);

            var loadedModule = loadedModules.LoadedModules[0];

            Assert.AreEqual(BaseAssemblyName, loadedModule.AssemblyName);
            Assert.AreEqual(BaseAssemblyVersion, loadedModule.Version);
            Assert.AreEqual(BaseAssemblyName, loadedModule.Data["namespace"]);
            Assert.AreEqual(BaseHashCode.ToString(), loadedModule.Data["assemblyHashCode"]);
            Assert.AreEqual(BasePublicKey, loadedModule.Data["publicKeyToken"]);
            Assert.AreEqual(BaseCompanyName, loadedModule.Data["Implementation-Vendor"]);
            Assert.AreEqual(BaseCopyrightValue, loadedModule.Data["copyright"]);
            Assert.False(loadedModule.Data.ContainsKey("sha1Checksum"));
            Assert.False(loadedModule.Data.ContainsKey("sha512Checksum"));
        }
    }

    public class TestAssembly : Assembly
    {
        private bool _isDynamic;

        private AssemblyName _assemblyName;

        private int _hashCode;

        private string _location;

        private List<object> _customAttributes = new List<object>();

        public TestAssembly()
        {

        }

        public AssemblyName SetAssemblyName
        {
            set { _assemblyName = value; }
        }

        public override AssemblyName GetName()
        {
            return _assemblyName;
        }

        public override bool IsDynamic => _isDynamic;

        public bool SetDynamic
        {
            set { _isDynamic = value; }
        }

        public int SetHashCode
        {
            set { _hashCode = value; }
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public string SetLocation
        {
            set { _location = value; }
        }

        public override string Location => _location;

        public void AddCustomAttribute(object attribute)
        {
            _customAttributes.Add(attribute);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            var objects = new List<object>();

            foreach (var attribute in _customAttributes)
            {
                if (attribute.GetType() == attributeType)
                {
                    objects.Add(attribute);
                }
            }

            return objects.ToArray();
        }
    }
}
