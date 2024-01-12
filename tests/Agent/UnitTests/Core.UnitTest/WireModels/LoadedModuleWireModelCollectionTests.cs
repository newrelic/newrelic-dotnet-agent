// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class LoadedModuleWireModelCollectionTests
    {
        private const string BaseAssemblyName = "MyTestAssembly";

        private string BaseAssemblyVersion;
        private string BaseCompanyName;
        private string BaseCopyrightValue;
        private int    BaseHashCode;
        private string BasePublicKeyToken;
        private string BasePublicKey;

        private AssemblyName _baseAssemblyName;
        private TestAssembly _baseTestAssembly;

        [SetUp]
        public void SetUp()
        {
            
            BaseAssemblyVersion = "1.0.0";
            BaseCompanyName = "MyCompany";
            BaseCopyrightValue = "Copyright 2008";
            BaseHashCode = 42;
            BasePublicKeyToken = "publickeytoken";
            BasePublicKey = "7075626C69636B6579746F6B656E";

            _baseAssemblyName = new AssemblyName();
            _baseAssemblyName.Name = BaseAssemblyName;
            _baseAssemblyName.Version = new Version(BaseAssemblyVersion);
            _baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

            _baseTestAssembly = new TestAssembly();
            _baseTestAssembly.SetAssemblyName = _baseAssemblyName;
            _baseTestAssembly.SetDynamic = true; // false uses on disk assembly and this won'y have one.
            _baseTestAssembly.SetHashCode = BaseHashCode;
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

        [Test]
        public void ErrorsHandled_TryGetAssemblyName_GetName()
        {
            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.ItemToTest = "GetName";

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(0, loadedModules.LoadedModules.Count);
        }

        [Test]
        public void ErrorsHandled_TryGetAssemblyName_IsDynamic()
        {
            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.ItemToTest = "IsDynamic";

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(0, loadedModules.LoadedModules.Count);
        }

        [Test]
        public void ErrorsHandled_TryGetAssemblyHashCode()
        {
            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.ItemToTest = "GetHashCode";

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(1, loadedModules.LoadedModules.Count);

            var loadedModule = loadedModules.LoadedModules[0];

            Assert.AreEqual(BaseAssemblyName, loadedModule.AssemblyName);
            Assert.AreEqual(BaseAssemblyVersion, loadedModule.Version);
            Assert.AreEqual(BaseAssemblyName, loadedModule.Data["namespace"]);
            Assert.False(loadedModule.Data.ContainsKey("assemblyHashCode"));
            Assert.AreEqual(BasePublicKey, loadedModule.Data["publicKeyToken"]);
            Assert.AreEqual(BaseCompanyName, loadedModule.Data["Implementation-Vendor"]);
            Assert.AreEqual(BaseCopyrightValue, loadedModule.Data["copyright"]);
            Assert.False(loadedModule.Data.ContainsKey("sha1Checksum"));
            Assert.False(loadedModule.Data.ContainsKey("sha512Checksum"));
        }

        [Test]
        public void ErrorsHandled_TryGetAssemblyName_Location()
        {
            _baseTestAssembly.SetDynamic = false;
            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.ItemToTest = "Location";

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(0, loadedModules.LoadedModules.Count);
        }

        [Test]
        public void ErrorsHandled_GetCustomAttributes()
        {
            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.ItemToTest = "GetCustomAttributes";

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(1, loadedModules.LoadedModules.Count);

            var loadedModule = loadedModules.LoadedModules[0];

            Assert.AreEqual(BaseAssemblyName, loadedModule.AssemblyName);
            Assert.AreEqual(BaseAssemblyVersion, loadedModule.Version);
            Assert.AreEqual(BaseAssemblyName, loadedModule.Data["namespace"]);
            Assert.AreEqual(BaseHashCode.ToString(), loadedModule.Data["assemblyHashCode"]);
            Assert.AreEqual(BasePublicKey, loadedModule.Data["publicKeyToken"]);
            Assert.False(loadedModule.Data.ContainsKey("Implementation-Vendor"));
            Assert.False(loadedModule.Data.ContainsKey("copyright"));
            Assert.False(loadedModule.Data.ContainsKey("sha1Checksum"));
            Assert.False(loadedModule.Data.ContainsKey("sha512Checksum"));
        }

        [Test]
        public void ErrorsHandled_GetCustomAttributes_HandlesNulls()
        {
            _baseTestAssembly = new TestAssembly();
            _baseTestAssembly.SetAssemblyName = _baseAssemblyName;
            _baseTestAssembly.SetDynamic = true; // false uses on disk assembly and this won'y have one.
            _baseTestAssembly.SetHashCode = BaseHashCode;


            _baseTestAssembly.AddCustomAttribute(new AssemblyCompanyAttribute(null));
            _baseTestAssembly.AddCustomAttribute(new AssemblyCopyrightAttribute(null));


            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.ItemToTest = "";

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(1, loadedModules.LoadedModules.Count);

            var loadedModule = loadedModules.LoadedModules[0];

            Assert.False(loadedModule.Data.ContainsKey("Implementation-Vendor"));
            Assert.False(loadedModule.Data.ContainsKey("copyright"));
        }

        [Test]
        public void ErrorsHandled_PublickeyToken()
        {
            var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
            evilAssembly.GetName().SetPublicKeyToken(null);

            var assemblies = new List<Assembly>();
            assemblies.Add(evilAssembly);

            var loadedModules = LoadedModuleWireModelCollection.Build(assemblies);

            Assert.AreEqual(1, loadedModules.LoadedModules.Count);

            var loadedModule = loadedModules.LoadedModules[0];

            Assert.AreEqual(BaseAssemblyName, loadedModule.AssemblyName);
            Assert.AreEqual(BaseAssemblyVersion, loadedModule.Version);
            Assert.AreEqual(BaseAssemblyName, loadedModule.Data["namespace"]);
            Assert.AreEqual(BaseHashCode.ToString(), loadedModule.Data["assemblyHashCode"]);
            Assert.False(loadedModule.Data.ContainsKey("publicKeyToken"));
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

        private List<object> _customAttributes = new List<object>();

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

    public class EvilTestAssembly : Assembly
    {
        private Assembly _assembly;

        public string ItemToTest;

        public EvilTestAssembly(Assembly assembly)
        {
            _assembly= assembly;
        }

        public override AssemblyName GetName()
        {
            if (ItemToTest != "GetName")
            {
                return _assembly.GetName();
            }

            throw new Exception();
        }

        public override bool IsDynamic
        {
            get
            {
                if (ItemToTest != "IsDynamic")
                {
                    return _assembly.IsDynamic;
                }

                throw new Exception();
            }
        }

        public override int GetHashCode()
        {
            if (ItemToTest != "GetHashCode")
            {
                return _assembly.GetHashCode();
            }

            throw new Exception();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            if (ItemToTest != "GetCustomAttributes")
            {
                return _assembly.GetCustomAttributes(attributeType, inherit);
            }

            throw new Exception();
        }
    }
}
