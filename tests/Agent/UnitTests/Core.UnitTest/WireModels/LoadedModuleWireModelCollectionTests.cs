// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels;

[TestFixture]
public class LoadedModuleWireModelCollectionTests
{
    private const string BaseAssemblyNamespace = "MyTestAssembly";
    private const string BaseAssemblyName = "MyTestAssembly.dll";

    private string BaseAssemblyVersion;
    private string BaseAssemblyPath;
    private string BaseCompanyName;
    private int    BaseHashCode;
    private string BasePublicKeyToken;
    private string BasePublicKey;
    private string Sha1Checksum;
    private string Sha512Checksum;
    private string BaseFileVersion;

    private AssemblyName _baseAssemblyName;
    private TestAssembly _baseTestAssembly;

    private IFileWrapper _fileWrapper;

    [SetUp]
    public void SetUp()
    {
            
        BaseAssemblyVersion = "1.0.0";
        BaseAssemblyPath = @"C:\path\to\assembly\MyTestAssembly.dll";
        BaseCompanyName = "MyCompany";
        BaseHashCode = 42;
        BasePublicKeyToken = "publickeytoken";
        BasePublicKey = "7075626C69636B6579746F6B656E";
        Sha1Checksum = "60cacbf3d72e1e7834203da608037b1bf83b40e8";
        Sha512Checksum = "8efb4f73c5655351c444eb109230c556d39e2c7624e9c11abc9e3fb4b9b9254218cc5085b454a9698d085cfa92198491f07a723be4574adc70617b73eb0b6461";
        BaseFileVersion = "1.0.0.0";

        _baseAssemblyName = new AssemblyName();
        _baseAssemblyName.Name = BaseAssemblyNamespace;
        _baseAssemblyName.Version = new Version(BaseAssemblyVersion);
        _baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

        _baseTestAssembly = new TestAssembly();
        _baseTestAssembly.SetAssemblyName = _baseAssemblyName;
        _baseTestAssembly.SetDynamic = false; // dynamic assemblies are not included so this must be false for most tests
        _baseTestAssembly.SetHashCode = BaseHashCode;
        _baseTestAssembly.SetLocation = BaseAssemblyPath;
        _baseTestAssembly.AddOrReplaceCustomAttribute(new AssemblyCompanyAttribute(BaseCompanyName));

        var fileStream = Mock.Create<FileStream>();
        Mock.Arrange(() => fileStream.Length).Returns(1024);
        Mock.Arrange(() => fileStream.Read(Arg.IsAny<byte[]>(), Arg.IsAny<int>(), Arg.IsAny<int>())).Returns(1024).InSequence(); // send some bytes
        Mock.Arrange(() => fileStream.Read(Arg.IsAny<byte[]>(), Arg.IsAny<int>(), Arg.IsAny<int>())).Returns(0).InSequence(); // FileStream.Read returns 0 when the end of the stream is reached.
        _fileWrapper = Mock.Create<IFileWrapper>();
        Mock.Arrange(() => _fileWrapper.Exists(Arg.IsAny<string>())).Returns(true);
        Mock.Arrange(() => _fileWrapper.Open(Arg.IsAny<string>(), Arg.IsAny<FileMode>(), Arg.IsAny<FileAccess>(), Arg.IsAny<FileShare>()))
            .Returns(fileStream);
        Mock.Arrange(() => _fileWrapper.GetFileVersion(Arg.IsAny<string>())).Returns(BaseFileVersion);
    }

    [TearDown] public void TearDown()
    {
        _baseAssemblyName = null;
        _baseTestAssembly= null;
    }

    [TestCase(BaseAssemblyNamespace, true, ExpectedResult = 0)]
    [TestCase(BaseAssemblyNamespace, false, ExpectedResult = 1)]
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

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        return loadedModules.LoadedModules.Count;
    }

    [Test]
    public void Excludes_ZeroVersioned_Assemblies()
    {
        var zeroVersioned = new ZeroVersionAssembly();
        zeroVersioned.SetAssemblyName = _baseAssemblyName;
        zeroVersioned.SetHashCode = BaseHashCode;
        zeroVersioned.SetLocation = BaseAssemblyPath;
        zeroVersioned.AddOrReplaceCustomAttribute(new AssemblyCompanyAttribute(BaseCompanyName));

        var assemblies = new List<Assembly>();
        assemblies.Add(zeroVersioned);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(0));
    }

    [Test]
    public void Excludes_Dynamic_Assemblies()
    {
        var assemblies = new List<Assembly>();
        _baseTestAssembly.SetDynamic = true;
        assemblies.Add(_baseTestAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(0));
    }

    [Test]
    public void ValidateAllData()
    {
        var assemblies = new List<Assembly>();
        assemblies.Add(_baseTestAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(1));

        var loadedModule = loadedModules.LoadedModules[0];

        Assert.Multiple(() =>
        {
            Assert.That(loadedModule.AssemblyName, Is.EqualTo(BaseAssemblyName));
            Assert.That(loadedModule.Version, Is.EqualTo(BaseAssemblyVersion));
            Assert.That(loadedModule.Data["namespace"], Is.EqualTo(BaseAssemblyNamespace));
            Assert.That(loadedModule.Data["assemblyHashCode"], Is.EqualTo(BaseHashCode.ToString()));
            Assert.That(loadedModule.Data["publicKeyToken"], Is.EqualTo(BasePublicKey));
            Assert.That(loadedModule.Data["Implementation-Vendor"], Is.EqualTo(BaseCompanyName));
            Assert.That(loadedModule.Data["sha1Checksum"], Is.EqualTo(Sha1Checksum));
            Assert.That(loadedModule.Data["sha512Checksum"], Is.EqualTo(Sha512Checksum));
            Assert.That(loadedModule.Data["fileVersion"], Is.EqualTo(BaseFileVersion));
        });
    }

    [Test]
    public void ErrorsHandled_TryGetAssemblyName_GetName()
    {
        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.ItemToTest = "GetName";

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Is.Empty);
    }

    [Test]
    public void ErrorsHandled_TryGetAssemblyName_IsDynamic()
    {
        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.ItemToTest = "IsDynamic";

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Is.Empty);
    }

    [Test]
    public void ErrorsHandled_TryGetAssemblyHashCode()
    {
        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.ItemToTest = "GetHashCode";

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(1));

        var loadedModule = loadedModules.LoadedModules[0];

        Assert.Multiple(() =>
        {
            Assert.That(loadedModule.AssemblyName, Is.EqualTo(BaseAssemblyName));
            Assert.That(loadedModule.Version, Is.EqualTo(BaseAssemblyVersion));
            Assert.That(loadedModule.Data["namespace"], Is.EqualTo(BaseAssemblyNamespace));
            Assert.That(loadedModule.Data["publicKeyToken"], Is.EqualTo(BasePublicKey));
            Assert.That(loadedModule.Data["Implementation-Vendor"], Is.EqualTo(BaseCompanyName));
            Assert.That(loadedModule.Data["sha1Checksum"], Is.EqualTo(Sha1Checksum));
            Assert.That(loadedModule.Data["sha512Checksum"], Is.EqualTo(Sha512Checksum));
            Assert.That(loadedModule.Data["fileVersion"], Is.EqualTo(BaseFileVersion));
            Assert.That(loadedModule.Data.ContainsKey("assemblyHashCode"), Is.False);
        });
    }

    [Test]
    public void ErrorsHandled_TryGetAssemblyName_Location()
    {
        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.ItemToTest = "Location";

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Is.Empty);
    }

    [Test]
    public void ErrorsHandled_GetCustomAttributes()
    {
        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.ItemToTest = "GetCustomAttributes";

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(1));

        var loadedModule = loadedModules.LoadedModules[0];

        Assert.Multiple(() =>
        {
            Assert.That(loadedModule.AssemblyName, Is.EqualTo(BaseAssemblyName));
            Assert.That(loadedModule.Version, Is.EqualTo(BaseAssemblyVersion));
            Assert.That(loadedModule.Data["namespace"], Is.EqualTo(BaseAssemblyNamespace));
            Assert.That(loadedModule.Data["assemblyHashCode"], Is.EqualTo(BaseHashCode.ToString()));
            Assert.That(loadedModule.Data["publicKeyToken"], Is.EqualTo(BasePublicKey));
            Assert.That(loadedModule.Data["sha1Checksum"], Is.EqualTo(Sha1Checksum));
            Assert.That(loadedModule.Data["sha512Checksum"], Is.EqualTo(Sha512Checksum));
            Assert.That(loadedModule.Data.ContainsKey("Implementation-Vendor"), Is.False);
            Assert.That(loadedModule.Data["fileVersion"], Is.EqualTo(BaseFileVersion));
        });
    }

    [Test]
    public void ErrorsHandled_GetCustomAttributes_HandlesNulls()
    {
        _baseTestAssembly.AddOrReplaceCustomAttribute(new AssemblyCompanyAttribute(null));

        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.ItemToTest = "";

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(1));

        var loadedModule = loadedModules.LoadedModules[0];

        Assert.That(loadedModule.Data.ContainsKey("Implementation-Vendor"), Is.False);
    }

    [Test]
    public void ErrorsHandled_PublickeyToken()
    {
        var evilAssembly = new EvilTestAssembly(_baseTestAssembly);
        evilAssembly.GetName().SetPublicKeyToken(null);

        var assemblies = new List<Assembly>();
        assemblies.Add(evilAssembly);

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        Assert.That(loadedModules.LoadedModules, Has.Count.EqualTo(1));

        var loadedModule = loadedModules.LoadedModules[0];

        Assert.Multiple(() =>
        {
            Assert.That(loadedModule.AssemblyName, Is.EqualTo(BaseAssemblyName));
            Assert.That(loadedModule.Version, Is.EqualTo(BaseAssemblyVersion));
            Assert.That(loadedModule.Data["namespace"], Is.EqualTo(BaseAssemblyNamespace));
            Assert.That(loadedModule.Data["assemblyHashCode"], Is.EqualTo(BaseHashCode.ToString()));
            Assert.That(loadedModule.Data["Implementation-Vendor"], Is.EqualTo(BaseCompanyName));
            Assert.That(loadedModule.Data["sha1Checksum"], Is.EqualTo(Sha1Checksum));
            Assert.That(loadedModule.Data["sha512Checksum"], Is.EqualTo(Sha512Checksum));
            Assert.That(loadedModule.Data["fileVersion"], Is.EqualTo(BaseFileVersion));
            Assert.That(loadedModule.Data.ContainsKey("publicKeyToken"), Is.False);
        });
    }
}

public class TestAssembly : Assembly
{
    private bool _isDynamic;

    private AssemblyName _assemblyName;

    private int _hashCode;

    private string _location;

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

    public string SetLocation
    {
        set { _location = value; }
    }

    public override string Location => _location;

    public void AddOrReplaceCustomAttribute(object attribute)
    {
        var attributeType = attribute.GetType();
        for (int i = 0; i < _customAttributes.Count; i++)
        {
            if (_customAttributes[i].GetType() == attributeType)
            {
                _customAttributes[i] = attribute;
                return;
            }
        }

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
                return false;
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

    public override string Location
    {
        get
        {
            if (ItemToTest != "Location")
            {
                return _assembly.Location;
            }

            throw new Exception();
        }
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

public class ZeroVersionAssembly : TestAssembly
{
    public override bool IsDynamic => false;

    public override AssemblyName GetName()
    {
        var assemblyName = base.GetName();
        assemblyName.Version = new Version("0.0.0.0");
        return assemblyName;
    }
}