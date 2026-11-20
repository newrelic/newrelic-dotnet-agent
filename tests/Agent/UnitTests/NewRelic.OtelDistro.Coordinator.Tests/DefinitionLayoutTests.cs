// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Coordinator.Tests;

[TestFixture]
public class DefinitionLayoutTests
{
    private const string SequentialUnicodeFields = "Sequential;Unicode;A:System.IntPtr,B:System.UInt16,C:System.Byte,D:System.UInt32";

    [Test]
    public void Describe_SequentialUnicode_ListsFieldNamesAndTypesInOrder()
    {
        Assert.That(DefinitionLayout.Describe(typeof(SequentialUnicode)), Is.EqualTo(SequentialUnicodeFields));
    }

    [Test]
    public void Describe_IgnoresStaticFields()
    {
        Assert.That(DefinitionLayout.Describe(typeof(WithStaticField)), Is.EqualTo("Sequential;Unicode;Value:System.UInt16"));
    }

    [Test]
    public void Describe_EnumField_UsesUnderlyingType()
    {
        Assert.That(DefinitionLayout.Describe(typeof(WithEnumField)), Is.EqualTo("Sequential;Unicode;Kind:System.Byte,Categories:System.UInt32"));
    }

    [Test]
    public void Describe_ExplicitAnsi()
    {
        Assert.That(DefinitionLayout.Describe(typeof(ExplicitAnsi)), Is.EqualTo("Explicit;Ansi;Value:System.Int32"));
    }

    [Test]
    public void Describe_Auto()
    {
        Assert.That(DefinitionLayout.Describe(typeof(AutoAuto)), Is.EqualTo("Auto;Auto;Value:System.Int32"));
    }

    [Test]
    public void Describe_ClassWithDefaults_IsAutoLayoutAnsi()
    {
        Assert.That(DefinitionLayout.Describe(typeof(DefaultClass)), Is.EqualTo("Auto;Ansi;Value:System.Int32"));
    }

    [Test]
    public void Describe_Null_Throws()
    {
        Assert.That(() => DefinitionLayout.Describe(null), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void DescribeMethod_Null_Throws()
    {
        Assert.That(() => DefinitionLayout.DescribeMethod(null), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void DescribeMethod_ArrayOfStructParam()
    {
        var method = typeof(DefinitionLayoutTests).GetMethod(nameof(ExternShaped), BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(DefinitionLayout.DescribeMethod(method), Is.EqualTo(
            "System.Int32(System.String,struct:" + SequentialUnicodeFields + "[],System.Int32,System.UInt32)"));
    }

    [Test]
    public void DescribeMethod_ArrayOfNonStructParam_UsesElementFullName()
    {
        var method = typeof(DefinitionLayoutTests).GetMethod(nameof(ArrayShaped), BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(DefinitionLayout.DescribeMethod(method), Is.EqualTo("System.Void(System.String[],System.Byte[],System.IntPtr[])"));
    }

    [Test]
    public void DescribeMethod_GenericParameter_FallsBackToTypeName()
    {
        var method = typeof(DefinitionLayoutTests).GetMethod(nameof(GenericShaped), BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(DefinitionLayout.DescribeMethod(method), Is.EqualTo("System.Void(T)"));
    }

    [Test]
    public void FirstDifference_Equal_None()
    {
        Assert.That(DefinitionLayout.FirstDifference(SequentialUnicodeFields, SequentialUnicodeFields), Is.EqualTo("none"));
    }

    [Test]
    public void FirstDifference_BothNull_None()
    {
        Assert.That(DefinitionLayout.FirstDifference(null, null), Is.EqualTo("none"));
    }

    [Test]
    public void FirstDifference_Layout()
    {
        Assert.That(DefinitionLayout.FirstDifference("Sequential;Unicode;System.Byte", "Explicit;Unicode;System.Byte"),
            Is.EqualTo("layout: expected Sequential, found Explicit"));
    }

    [Test]
    public void FirstDifference_Charset()
    {
        Assert.That(DefinitionLayout.FirstDifference("Sequential;Unicode;System.Byte", "Sequential;Ansi;System.Byte"),
            Is.EqualTo("charset: expected Unicode, found Ansi"));
    }

    [Test]
    public void FirstDifference_FieldCount()
    {
        Assert.That(DefinitionLayout.FirstDifference("Sequential;Unicode;A:System.Byte,B:System.Byte", "Sequential;Unicode;A:System.Byte"),
            Is.EqualTo("field count: expected 2, found 1"));
    }

    [Test]
    public void FirstDifference_FieldCount_ToZeroFields()
    {
        Assert.That(DefinitionLayout.FirstDifference("Sequential;Unicode;A:System.Byte", "Sequential;Unicode;"),
            Is.EqualTo("field count: expected 1, found 0"));
    }

    [Test]
    public void FirstDifference_ReportsFirstDifferingField()
    {
        Assert.That(DefinitionLayout.FirstDifference("Sequential;Unicode;A:System.IntPtr,B:System.UInt16,C:System.Byte", "Sequential;Unicode;A:System.IntPtr,C:System.Byte,B:System.UInt16"),
            Is.EqualTo("field 1: expected B:System.UInt16, found C:System.Byte"));
    }

    [Test]
    public void FirstDifference_SameTypeReorder_Detected()
    {
        Assert.That(DefinitionLayout.FirstDifference(
                "Sequential;Unicode;TargetType:System.IntPtr,TargetMethod:System.IntPtr,Kind:System.Byte",
                "Sequential;Unicode;TargetMethod:System.IntPtr,TargetType:System.IntPtr,Kind:System.Byte"),
            Is.EqualTo("field 0: expected TargetType:System.IntPtr, found TargetMethod:System.IntPtr"));
    }

    [Test]
    public void Describe_SameTypeFieldsSwapped_GiveDifferentSignatures()
    {
        Assert.That(DefinitionLayout.Describe(typeof(SwappedA)), Is.Not.EqualTo(DefinitionLayout.Describe(typeof(SwappedB))));
    }

    [Test]
    public void FirstDifference_LayoutWinsOverCharsetAndFields()
    {
        Assert.That(DefinitionLayout.FirstDifference("Sequential;Unicode;System.Byte", "Auto;Ansi;System.Int32,System.Int32"),
            Is.EqualTo("layout: expected Sequential, found Auto"));
    }

    [TestCase(null, "Sequential;Unicode;System.Byte", "expected null, found Sequential;Unicode;System.Byte")]
    [TestCase("Sequential;Unicode;System.Byte", null, "expected Sequential;Unicode;System.Byte, found null")]
    [TestCase("garbage", "Sequential;Unicode;System.Byte", "expected garbage, found Sequential;Unicode;System.Byte")]
    [TestCase("Sequential;Unicode;System.Byte", "Sequential;Unicode", "expected Sequential;Unicode;System.Byte, found Sequential;Unicode")]
    [TestCase("System.Int32(System.String)", "System.Void(System.String)", "expected System.Int32(System.String), found System.Void(System.String)")]
    [TestCase("System.Int32(struct:Sequential;Unicode;System.Byte[])", "System.Int32(struct:Sequential;Ansi;System.Byte[])",
        "expected System.Int32(struct:Sequential;Unicode;System.Byte[]), found System.Int32(struct:Sequential;Ansi;System.Byte[])")]
    public void FirstDifference_NullOrMalformed(string expected, string actual, string difference)
    {
        Assert.That(DefinitionLayout.FirstDifference(expected, actual), Is.EqualTo(difference));
    }

    // Shapes of the real extern: (string, array of struct, int, uint) -> int.
    private static int ExternShaped(string id, SequentialUnicode[] items, int size, uint categories) => 0;

    private static void GenericShaped<T>(T value)
    {
    }

    private static void ArrayShaped(string[] names, byte[] bytes, IntPtr[] pointers)
    {
    }

    private enum ByteEnum : byte
    {
        None = 0
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SequentialUnicode
    {
        public IntPtr A;
        public ushort B;
        public byte C;
        public uint D;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SwappedA
    {
        public IntPtr First;
        public IntPtr Second;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SwappedB
    {
        public IntPtr Second;
        public IntPtr First;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WithStaticField
    {
        public static int Ignored;
        public static readonly string AlsoIgnored = "x";
        public ushort Value;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WithEnumField
    {
        public ByteEnum Kind;
        public uint Categories;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi)]
    private struct ExplicitAnsi
    {
        [FieldOffset(0)]
        public int Value;
    }

    [StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
    private struct AutoAuto
    {
        public int Value;
    }

    private class DefaultClass
    {
        public int Value = 1;
    }
}
