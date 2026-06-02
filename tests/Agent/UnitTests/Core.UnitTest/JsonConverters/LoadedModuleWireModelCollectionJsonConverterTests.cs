// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Core.WireModels;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Utilities;

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
    private const string BaseFileVersion = "1.0.0.0";

    private IFileWrapper _fileWrapper;

    [SetUp]
    public void SetUp()
    {
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

    [Test]
    public void LoadedModuleWireModelCollectionIsJsonSerializable()
    {
        var expected = @"[""Jars"",[[""MyTestAssembly.dll"",""1.0.0"",{""namespace"":""MyTestAssembly"",""publicKeyToken"":""7075626C69636B6579746F6B656E"",""sha1Checksum"":""60cacbf3d72e1e7834203da608037b1bf83b40e8"",""sha512Checksum"":""8efb4f73c5655351c444eb109230c556d39e2c7624e9c11abc9e3fb4b9b9254218cc5085b454a9698d085cfa92198491f07a723be4574adc70617b73eb0b6461"",""assemblyHashCode"":""42"",""Implementation-Vendor"":""MyCompany"",""fileVersion"":""1.0.0.0""}]]]";

        var baseAssemblyName = new AssemblyName();
        baseAssemblyName.Name = BaseAssemblyName;
        baseAssemblyName.Version = new Version(BaseAssemblyVersion);
        baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

        var baseTestAssembly = new TestAssembly();
        baseTestAssembly.SetAssemblyName = baseAssemblyName;
        baseTestAssembly.SetHashCode = BaseHashCode;
        baseTestAssembly.SetLocation = BaseAssemblyPath;
        baseTestAssembly.AddOrReplaceCustomAttribute(new AssemblyCompanyAttribute(BaseCompanyName));

        var assemblies = new List<Assembly>();
        assemblies.Add(baseTestAssembly);
        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        var serialized = JsonConvert.SerializeObject(new[] { loadedModules }, Formatting.None);
        Assert.That(serialized, Is.EqualTo(expected));
    }

    [Test]
    public void LoadedModuleWireModelCollectionHandlesNulls()
    {
        var expected = @"[""Jars"",[[""MyTestAssembly.dll"",""1.0.0"",{""namespace"":""MyTestAssembly"",""publicKeyToken"":""7075626C69636B6579746F6B656E"",""sha1Checksum"":""60cacbf3d72e1e7834203da608037b1bf83b40e8"",""sha512Checksum"":""8efb4f73c5655351c444eb109230c556d39e2c7624e9c11abc9e3fb4b9b9254218cc5085b454a9698d085cfa92198491f07a723be4574adc70617b73eb0b6461"",""assemblyHashCode"":""42"",""fileVersion"":""1.0.0.0""}]]]";

        var baseAssemblyName = new AssemblyName();
        baseAssemblyName.Name = BaseAssemblyName;
        baseAssemblyName.Version = new Version(BaseAssemblyVersion);
        baseAssemblyName.SetPublicKeyToken(Encoding.ASCII.GetBytes(BasePublicKeyToken));

        var baseTestAssembly = new TestAssembly();
        baseTestAssembly.SetAssemblyName = baseAssemblyName;
        baseTestAssembly.SetHashCode = BaseHashCode;
        baseTestAssembly.SetLocation = BaseAssemblyPath;
        baseTestAssembly.AddOrReplaceCustomAttribute(new AssemblyCompanyAttribute(null));

        var assemblies = new List<Assembly> { baseTestAssembly };

        var loadedModules = LoadedModuleWireModelCollection.Build(assemblies, _fileWrapper);

        var serialized = JsonConvert.SerializeObject(new[] { loadedModules }, Formatting.None);
        Assert.That(serialized, Is.EqualTo(expected));
    }
}