// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class DictionaryHelpersTests
{
    // Example: "{\"Host\":\"localhost:7071\",\"traceparent\":\"00-8141368177692588f683b7e7ce8db2a7-bba89a27c8c69cb0-00\"}"
    [TestCase("{\"Host\":\"localhost:7071\",\"traceparent\":\"00-8141368177692588f683b7e7ce8db2a7-bba89a27c8c69cb0-00\"}")]
    [TestCase("{\"Host\":42,\"traceparent\":{\"Test\":\"Code\"}}")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("42")]
    public void Successfuly_Returns_Dictionary_No_Errors(string test)
    {
        var result = DictionaryHelpers.FromJson(test);

        Assert.That(result, Is.Not.Null);

        if (!string.IsNullOrEmpty(test) && test.Contains("traceparent"))
        {
            Assert.That(result["traceparent"], Is.Not.Null);
        }
    }

    [Test]
    public void Lookup_Is_Case_Insensitive()
    {
        var result = DictionaryHelpers.FromJson("{\"Host\":\"localhost:7071\",\"TraceParent\":\"abc\"}");

        Assert.Multiple(() =>
        {
            Assert.That(result["host"], Is.EqualTo("localhost:7071"));
            Assert.That(result["HOST"], Is.EqualTo("localhost:7071"));
            Assert.That(result["traceparent"], Is.EqualTo("abc"));
            Assert.That(result.ContainsKey("TRACEPARENT"), Is.True);
        });
    }

    [Test]
    public void Keys_Differing_Only_By_Case_Keep_Last_Value()
    {
        var result = DictionaryHelpers.FromJson("{\"Host\":\"first\",\"host\":\"second\"}");

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result["HOST"], Is.EqualTo("second"));
        });
    }

    [TestCase("null")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("42")]
    [TestCase("{not json")]
    public void Empty_Or_Invalid_Json_Returns_Empty_Case_Insensitive_Dictionary(string test)
    {
        var result = DictionaryHelpers.FromJson(test);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
        Assert.That(result.ContainsKey("Host"), Is.False);
        Assert.That(result.TryGetValue("host", out _), Is.False);
    }

    [Test]
    public void Empty_Result_Is_Case_Insensitive_When_Further_Populated()
    {
        var result = (Dictionary<string, object>)DictionaryHelpers.FromJson("null");

        result["Host"] = "x";

        Assert.That(result.ContainsKey("host"), Is.True);
    }
}