// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Reflection;

namespace NewRelic.OtelDistro.Coordinator;

/// <summary>
/// Describes the marshalling-relevant shape of a native interop struct (or a P/Invoke signature) as a string, and
/// names the first difference between two such descriptions. Metadata only (no <c>Marshal.SizeOf</c>), so it also
/// works on types read through a MetadataLoadContext, which lets a build-time test compare the coordinator's copy of
/// OTel's native struct with OTel's own.
/// </summary>
public static class DefinitionLayout
{
    private const string NullText = "null";
    private const string StructPrefix = "struct:";
    private const char SectionSeparator = ';';
    private const char ListSeparator = ',';
    private const char FieldSeparator = ':';

    // Indexed by (attributes & LayoutMask) >> 3 and (attributes & StringFormatMask) >> 16.
    private static readonly string[] LayoutNames = { "Auto", "Sequential", "Explicit", "Invalid" };
    private static readonly string[] CharsetNames = { "Ansi", "Unicode", "Auto", "Custom" };

    /// <summary>
    /// <c>"&lt;layout&gt;;&lt;charset&gt;;&lt;field name&gt;:&lt;field type FullName&gt;,..."</c>: layout and charset from
    /// the type's attributes, then the instance fields in metadata order (static fields excluded; an enum field is
    /// written as its underlying type). Field names are part of the signature so that swapping or repurposing two
    /// fields of the same type is caught.
    /// </summary>
    /// <param name="structType">The struct to describe.</param>
    /// <returns>The signature.</returns>
    public static string Describe(Type structType)
    {
        if (structType == null)
        {
            throw new ArgumentNullException(nameof(structType));
        }

        var layout = LayoutNames[(int)(structType.Attributes & TypeAttributes.LayoutMask) >> 3];
        var charset = CharsetNames[(int)(structType.Attributes & TypeAttributes.StringFormatMask) >> 16];
        var fields = structType
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(f => f.MetadataToken)
            .Select(f => f.Name + FieldSeparator + TypeName(f.FieldType.IsEnum ? f.FieldType.GetEnumUnderlyingType() : f.FieldType));

        return layout + SectionSeparator + charset + SectionSeparator + string.Join(ListSeparator.ToString(), fields);
    }

    /// <summary>
    /// <c>"&lt;return type FullName&gt;(&lt;param FullName&gt;,...)"</c>. An array parameter is written
    /// <c>"&lt;element FullName&gt;[]"</c>; when the element is a struct it is <c>"struct:" + Describe(element) + "[]"</c>.
    /// </summary>
    /// <param name="method">The method, e.g. a P/Invoke extern.</param>
    /// <returns>The signature.</returns>
    public static string DescribeMethod(MethodInfo method)
    {
        if (method == null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var parameters = method.GetParameters().Select(p => DescribeParameter(p.ParameterType));
        return TypeName(method.ReturnType) + "(" + string.Join(ListSeparator.ToString(), parameters) + ")";
    }

    /// <summary>
    /// The first difference between two signatures from <see cref="Describe"/> (<c>layout</c>, <c>charset</c>,
    /// <c>field count</c>, then <c>field &lt;i&gt;</c> with a 0-based index), or <c>none</c>. Anything that is not a
    /// layout signature (a method signature, a null, a malformed string) gets <c>expected &lt;a&gt;, found &lt;b&gt;</c>.
    /// Never throws.
    /// </summary>
    /// <param name="expected">The coordinator's signature.</param>
    /// <param name="actual">OTel's signature.</param>
    /// <returns>The difference text.</returns>
    public static string FirstDifference(string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return "none";
        }

        if (!TrySplit(expected, out var expectedParts) || !TrySplit(actual, out var actualParts))
        {
            return Mismatch(expected, actual);
        }

        if (expectedParts[0] != actualParts[0])
        {
            return "layout: " + Mismatch(expectedParts[0], actualParts[0]);
        }

        if (expectedParts[1] != actualParts[1])
        {
            return "charset: " + Mismatch(expectedParts[1], actualParts[1]);
        }

        var expectedFields = SplitFields(expectedParts[2]);
        var actualFields = SplitFields(actualParts[2]);
        if (expectedFields.Length != actualFields.Length)
        {
            return "field count: " + Mismatch(expectedFields.Length.ToString(), actualFields.Length.ToString());
        }

        var index = 0;
        while (expectedFields[index] == actualFields[index])
        {
            index++;
        }

        return "field " + index + ": " + Mismatch(expectedFields[index], actualFields[index]);
    }

    private static string DescribeParameter(Type parameterType)
    {
        if (!parameterType.IsArray)
        {
            return TypeName(parameterType);
        }

        var element = parameterType.GetElementType();
        var isStruct = element.IsValueType && !element.IsPrimitive && !element.IsEnum;
        return (isStruct ? StructPrefix + Describe(element) : TypeName(element)) + "[]";
    }

    private static string TypeName(Type type) => type.FullName ?? type.Name;

    private static string Mismatch(string expected, string actual) =>
        "expected " + (expected ?? NullText) + ", found " + (actual ?? NullText);

    // A layout signature has three sections and no method parentheses; a method signature carries a ';' inside its
    // "struct:" parameter, so the parenthesis check keeps it out of the layout comparison.
    private static bool TrySplit(string signature, out string[] parts)
    {
        parts = signature == null || signature.IndexOf('(') >= 0 ? null : signature.Split(new[] { SectionSeparator }, 3);
        return parts != null && parts.Length == 3;
    }

    private static string[] SplitFields(string fields) =>
        fields.Length == 0 ? new string[0] : fields.Split(ListSeparator);
}
