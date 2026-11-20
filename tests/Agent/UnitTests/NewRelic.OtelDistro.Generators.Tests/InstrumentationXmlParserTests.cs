// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class InstrumentationXmlParserTests
{
    private const string Step0Xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<extension>
  <instrumentation>
    <tracerFactory name=""NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper"">
      <match assemblyName=""Step0App"" className=""Step0App.Worker"">
        <exactMethodMatcher methodName=""DoWork"" parameters=""System.Int32"" />
      </match>
    </tracerFactory>
  </instrumentation>
</extension>";

    [Test]
    public void Parse_Step0Xml_ReturnsOnePoint()
    {
        var result = InstrumentationXmlParser.Parse(Step0Xml);

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Points, Has.Count.EqualTo(1));

        var point = result.Points[0];
        Assert.Multiple(() =>
        {
            Assert.That(point.TracerFactoryName, Is.EqualTo("NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper"));
            Assert.That(point.AssemblyName, Is.EqualTo("Step0App"));
            Assert.That(point.ClassName, Is.EqualTo("Step0App.Worker"));
            Assert.That(point.MethodName, Is.EqualTo("DoWork"));
            Assert.That(point.ParameterTypes, Is.EqualTo(new[] { "System.Int32" }));
        });
    }

    [Test]
    public void Parse_NoFactoryName_UsesDefaultTracerFactory()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory>
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters="""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(1));
        Assert.That(result.Points[0].TracerFactoryName, Is.EqualTo(InstrumentationXmlParser.DefaultTracerFactoryName));
    }

    [Test]
    public void Parse_NoParametersAttribute_ParameterTypesNull()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(1));
        Assert.That(result.Points[0].ParameterTypes, Is.Null);
    }

    [Test]
    public void Parse_EmptyParameters_ZeroParameters()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters="""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(1));
        Assert.That(result.Points[0].ParameterTypes, Is.Not.Null);
        Assert.That(result.Points[0].ParameterTypes, Is.Empty);
    }

    [Test]
    public void Parse_ParameterTypesAttribute_IsIgnored()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameterTypes=""System.Int32"" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(1));
        Assert.That(result.Points[0].ParameterTypes, Is.Null);
    }

    [Test]
    public void Parse_GenericParameterWithCommas_SplitsOnTopLevelCommasOnly()
    {
        // Microsoft.Extensions.Logging.Logger.Log<TState>: five parameters, the last a generic instance whose type
        // arguments hold commas. OTel's native matcher renders the same strings (GENERICINST as Name`N[A,B], MVAR as !!n).
        var result = InstrumentationXmlParser.Parse(XmlWithParameters("Microsoft.Extensions.Logging.LogLevel,Microsoft.Extensions.Logging.EventId,!!0,System.Exception,System.Func`3[!!0,System.Exception,System.String]"));

        Assert.That(result.Points[0].ParameterTypes, Is.EqualTo(new[]
        {
            "Microsoft.Extensions.Logging.LogLevel",
            "Microsoft.Extensions.Logging.EventId",
            "!!0",
            "System.Exception",
            "System.Func`3[!!0,System.Exception,System.String]"
        }));
    }

    [Test]
    public void Parse_NestedGenericParameters_SplitsOnTopLevelCommasOnly()
    {
        // Confluent.Kafka Producer.Produce: a generic instance nested in another one.
        var result = InstrumentationXmlParser.Parse(XmlWithParameters("Confluent.Kafka.TopicPartition,Confluent.Kafka.Message`2[!0,!1],System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"));

        Assert.That(result.Points[0].ParameterTypes, Is.EqualTo(new[]
        {
            "Confluent.Kafka.TopicPartition",
            "Confluent.Kafka.Message`2[!0,!1]",
            "System.Action`1[Confluent.Kafka.DeliveryReport`2[!0,!1]]"
        }));
    }

    [Test]
    public void Parse_SpacesAroundTopLevelCommas_AreTrimmed()
    {
        // The OpenAI XML writes ", " between parameters.
        var result = InstrumentationXmlParser.Parse(XmlWithParameters("System.Collections.Generic.IEnumerable`1[OpenAI.Chat.ChatMessage], OpenAI.Chat.ChatCompletionOptions, System.Threading.CancellationToken"));

        Assert.That(result.Points[0].ParameterTypes, Is.EqualTo(new[]
        {
            "System.Collections.Generic.IEnumerable`1[OpenAI.Chat.ChatMessage]",
            "OpenAI.Chat.ChatCompletionOptions",
            "System.Threading.CancellationToken"
        }));
    }

    [Test]
    public void Parse_UnbalancedBrackets_KeepTheRestAsOneParameter()
    {
        // An unclosed '[' keeps every later comma inside it; a stray ']' does not take the depth below zero.
        var unclosed = InstrumentationXmlParser.Parse(XmlWithParameters("System.Int32,System.Func`2[System.String,System.Int32"));
        var stray = InstrumentationXmlParser.Parse(XmlWithParameters("System.Int32],System.String"));

        Assert.Multiple(() =>
        {
            Assert.That(unclosed.Points[0].ParameterTypes, Is.EqualTo(new[] { "System.Int32", "System.Func`2[System.String,System.Int32" }));
            Assert.That(stray.Points[0].ParameterTypes, Is.EqualTo(new[] { "System.Int32]", "System.String" }));
        });
    }

    private static string XmlWithParameters(string parameters) => @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"">
                <exactMethodMatcher methodName=""M"" parameters=""" + parameters + @""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

    [Test]
    public void Parse_MultipleFactoriesAndMatches_FlattensAll()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F1"">
              <match assemblyName=""A1"" className=""B1"">
                <exactMethodMatcher methodName=""M1"" parameters="""" />
                <exactMethodMatcher methodName=""M2"" parameters=""System.String"" />
              </match>
              <match assemblyName=""A2"" className=""B2"">
                <exactMethodMatcher methodName=""M3"" parameters=""System.Int32,System.String"" />
              </match>
            </tracerFactory>
            <tracerFactory name=""F2"">
              <match assemblyName=""A3"" className=""B3"">
                <exactMethodMatcher methodName=""M4"" parameters="""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(4));
        Assert.That(result.Points.Select(p => p.MethodName), Is.EqualTo(new[] { "M1", "M2", "M3", "M4" }));
        Assert.That(result.Points.First(p => p.MethodName == "M3").ParameterTypes, Is.EqualTo(new[] { "System.Int32", "System.String" }));
    }

    [Test]
    public void Parse_MalformedXml_ReturnsErrorAndNoPoints()
    {
        const string xml = "<extension><instrumentation><unclosed></instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Is.Empty);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
    }

    [Test]
    public void Parse_XmlWithNamespace_StillParsed()
    {
        const string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<extension xmlns=""urn:newrelic-extension"">
  <instrumentation>
    <tracerFactory name=""F"">
      <match assemblyName=""A"" className=""B"">
        <exactMethodMatcher methodName=""M"" parameters=""System.Int32"" />
      </match>
    </tracerFactory>
  </instrumentation>
</extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Points, Has.Count.EqualTo(1));
        Assert.That(result.Points[0].AssemblyName, Is.EqualTo("A"));
    }

    private static string FactoryXml(string factoryAttributes, string matchAttributes) => $@"<extension><instrumentation>
        <tracerFactory name=""F"" {factoryAttributes}>
          <match assemblyName=""A"" className=""B"" {matchAttributes}>
            <exactMethodMatcher methodName=""M1"" parameters="""" />
            <exactMethodMatcher methodName=""M2"" parameters=""System.Int32"" />
          </match>
        </tracerFactory>
      </instrumentation></extension>";

    // Version range -- InstrumentationConfiguration.h:302-303 reads minVersion/maxVersion from the <match> node.

    [Test]
    public void Parse_MatchWithMinAndMaxVersion_EveryPointOfTheMatchCarriesTheRawText()
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml("", @"minVersion=""1.2.3"" maxVersion=""4.5.6.7"""));

        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.Points.Select(p => p.MinVersion), Is.All.EqualTo("1.2.3"));
            Assert.That(result.Points.Select(p => p.MaxVersion), Is.All.EqualTo("4.5.6.7"));
        });
    }

    [Test]
    public void Parse_MatchWithoutVersionAttributes_VersionsAreNull()
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml("", ""));

        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.Points.Select(p => p.MinVersion), Is.All.Null);
            Assert.That(result.Points.Select(p => p.MaxVersion), Is.All.Null);
        });
    }

    [Test]
    public void Parse_OnlyMinVersion_MaxVersionIsNull()
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml("", @"minVersion=""2.0.0"""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Points[0].MinVersion, Is.EqualTo("2.0.0"));
            Assert.That(result.Points[0].MaxVersion, Is.Null);
        });
    }

    [Test]
    public void Parse_OnlyMaxVersion_MinVersionIsNull()
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml("", @"maxVersion=""3.0.0"""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Points[0].MinVersion, Is.Null);
            Assert.That(result.Points[0].MaxVersion, Is.EqualTo("3.0.0"));
        });
    }

    [Test]
    public void Parse_EmptyVersionAttributes_KeptAsEmptyText()
    {
        // The profiler's GetAttributeOrEmptyString cannot tell absent from empty; the raw text is carried as-is
        // and the consumer (VersionRangeConverter) decides what an empty bound means.
        var result = InstrumentationXmlParser.Parse(FactoryXml("", @"minVersion="""" maxVersion="""""));

        Assert.Multiple(() =>
        {
            Assert.That(result.Points[0].MinVersion, Is.EqualTo(""));
            Assert.That(result.Points[0].MaxVersion, Is.EqualTo(""));
        });
    }

    [Test]
    public void Parse_VersionRangesDifferPerMatch_EachMatchKeepsItsOwn()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""B"" minVersion=""1.0.0"" maxVersion=""2.0.0"">
                <exactMethodMatcher methodName=""M"" parameters="""" />
              </match>
              <match assemblyName=""A"" className=""B"" minVersion=""2.0.0"">
                <exactMethodMatcher methodName=""M"" parameters="""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.Points[0].MinVersion, Is.EqualTo("1.0.0"));
            Assert.That(result.Points[0].MaxVersion, Is.EqualTo("2.0.0"));
            Assert.That(result.Points[1].MinVersion, Is.EqualTo("2.0.0"));
            Assert.That(result.Points[1].MaxVersion, Is.Null);
        });
    }

    // enabled -- InstrumentationConfiguration.h:264-268: the profiler drops the factory only when the attribute value
    // equals "false" case-insensitively (Strings::AreEqualCaseInsensitive, exact length, no trimming). The parser keeps
    // such points and flags them Enabled == false.

    [TestCase("false")]
    [TestCase("FALSE")]
    [TestCase("False")]
    [TestCase("fAlSe")]
    public void Parse_EnabledFalseInAnyCase_PointsCarryEnabledFalse(string enabled)
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml($@"enabled=""{enabled}""", ""));

        Assert.That(result.Errors, Is.Empty);
        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.That(result.Points.Select(p => p.Enabled), Is.All.False);
    }

    [TestCase("true")]
    [TestCase("TRUE")]
    [TestCase("0")]
    [TestCase("")]
    [TestCase(" false")]
    [TestCase("false ")]
    [TestCase("no")]
    public void Parse_EnabledAnyOtherValue_PointsEnabled(string enabled)
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml($@"enabled=""{enabled}""", ""));

        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.That(result.Points.Select(p => p.Enabled), Is.All.True);
    }

    [Test]
    public void Parse_EnabledAbsent_PointsEnabled()
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml("", ""));

        Assert.That(result.Points, Has.Count.EqualTo(2));
        Assert.That(result.Points.Select(p => p.Enabled), Is.All.True);
    }

    [Test]
    public void Parse_DisabledFactoryAmongEnabledOnes_OnlyItsPointsDisabled()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F1"">
              <match assemblyName=""A1"" className=""B1""><exactMethodMatcher methodName=""M1"" parameters="""" /></match>
            </tracerFactory>
            <tracerFactory name=""F2"" enabled=""false"">
              <match assemblyName=""A2"" className=""B2""><exactMethodMatcher methodName=""M2"" parameters="""" /></match>
            </tracerFactory>
            <tracerFactory name=""F3"" enabled=""true"">
              <match assemblyName=""A3"" className=""B3""><exactMethodMatcher methodName=""M3"" parameters="""" /></match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points.Select(p => p.MethodName), Is.EqualTo(new[] { "M1", "M2", "M3" }));
        Assert.That(result.Points.Select(p => p.Enabled), Is.EqualTo(new[] { true, false, true }));
    }

    // className -- InstrumentationConfiguration.h:430-464 splits on commas outside <> and [] brackets, with no
    // trimming and no dropping of empty segments.

    private static string ClassNameXml(string className, string matchAttributes = "") => $@"<extension><instrumentation>
        <tracerFactory name=""F"" metricName=""Custom/Metric"">
          <match assemblyName=""A"" className=""{System.Security.SecurityElement.Escape(className)}"" {matchAttributes}>
            <exactMethodMatcher methodName=""M1"" parameters="""" />
            <exactMethodMatcher methodName=""M2"" parameters=""System.Int32,System.String"" />
          </match>
        </tracerFactory>
      </instrumentation></extension>";

    [Test]
    public void Parse_SingleClassName_NotSplit()
    {
        var result = InstrumentationXmlParser.Parse(ClassNameXml("N.A"));

        Assert.That(result.Points.Select(p => p.ClassName), Is.EqualTo(new[] { "N.A", "N.A" }));
    }

    [Test]
    public void Parse_CommaSeparatedClassNames_OnePointPerClassWithIdenticalOtherFields()
    {
        var result = InstrumentationXmlParser.Parse(ClassNameXml("N.A,N.B", @"minVersion=""1.0.0"" maxVersion=""2.0.0"""));

        Assert.That(result.Points, Has.Count.EqualTo(4));
        Assert.Multiple(() =>
        {
            Assert.That(result.Points.Select(p => p.ClassName), Is.EquivalentTo(new[] { "N.A", "N.A", "N.B", "N.B" }));
            foreach (var className in new[] { "N.A", "N.B" })
            {
                var forClass = result.Points.Where(p => p.ClassName == className).ToList();
                Assert.That(forClass.Select(p => p.MethodName), Is.EqualTo(new[] { "M1", "M2" }));
                Assert.That(forClass[0].ParameterTypes, Is.Empty);
                Assert.That(forClass[1].ParameterTypes, Is.EqualTo(new[] { "System.Int32", "System.String" }));
                Assert.That(forClass.Select(p => p.AssemblyName), Is.All.EqualTo("A"));
                Assert.That(forClass.Select(p => p.TracerFactoryName), Is.All.EqualTo("F"));
                Assert.That(forClass.Select(p => p.MetricName), Is.All.EqualTo("Custom/Metric"));
                Assert.That(forClass.Select(p => p.MinVersion), Is.All.EqualTo("1.0.0"));
                Assert.That(forClass.Select(p => p.MaxVersion), Is.All.EqualTo("2.0.0"));
            }
        });
    }

    [Test]
    public void Parse_ClassNamesInDeclarationOrder_ForEachMethod()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"" className=""N.A,N.B,N.C"">
                <exactMethodMatcher methodName=""M"" parameters="""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points.Select(p => p.ClassName), Is.EqualTo(new[] { "N.A", "N.B", "N.C" }));
    }

    [Test]
    public void Parse_ClassNamesWithSpaceAndTrailingComma_SplitsLikeTheProfiler()
    {
        // Profiler: no trimming (" N.B" keeps its leading space) and the empty segment after the trailing comma is
        // kept, so "N.A, N.B," yields "N.A", " N.B" and "".
        var result = InstrumentationXmlParser.Parse(ClassNameXml("N.A, N.B,"));

        Assert.That(result.Points.Where(p => p.MethodName == "M1").Select(p => p.ClassName), Is.EqualTo(new[] { "N.A", " N.B", "" }));
    }

    [Test]
    public void Parse_ClassNamesWithConsecutiveAndLeadingCommas_KeepsEmptySegments()
    {
        var result = InstrumentationXmlParser.Parse(ClassNameXml(",N.A,,N.B"));

        Assert.That(result.Points.Where(p => p.MethodName == "M1").Select(p => p.ClassName), Is.EqualTo(new[] { "", "N.A", "", "N.B" }));
    }

    [Test]
    public void Parse_DuplicateClassNames_NotDeduplicated()
    {
        // The profiler's std::set holds distinct shared_ptr objects, so equal class names are not collapsed.
        var result = InstrumentationXmlParser.Parse(ClassNameXml("N.A,N.A"));

        Assert.That(result.Points.Where(p => p.MethodName == "M1").Select(p => p.ClassName), Is.EqualTo(new[] { "N.A", "N.A" }));
    }

    [TestCase("N.Outer<A,B>,N.Other", new[] { "N.Outer<A,B>", "N.Other" })]
    [TestCase("N.Outer[A,B],N.Other", new[] { "N.Outer[A,B]", "N.Other" })]
    [TestCase("N.Outer<A,List<B,C>>,N.Other", new[] { "N.Outer<A,List<B,C>>", "N.Other" })]
    [TestCase("N.A,N.Outer<B,C>", new[] { "N.A", "N.Outer<B,C>" })]
    public void Parse_CommasInsideBrackets_DoNotSplit(string className, string[] expected)
    {
        var result = InstrumentationXmlParser.Parse(ClassNameXml(className));

        Assert.That(result.Points.Where(p => p.MethodName == "M1").Select(p => p.ClassName), Is.EqualTo(expected));
    }

    [Test]
    public void Parse_UnbalancedClosingBracket_NeverSplitsAgainLikeTheProfiler()
    {
        // The profiler's bracket level is a uint32_t, so a stray '>' wraps it to a huge value and no later comma splits.
        var result = InstrumentationXmlParser.Parse(ClassNameXml("N.A>,N.B"));

        Assert.That(result.Points.Where(p => p.MethodName == "M1").Select(p => p.ClassName), Is.EqualTo(new[] { "N.A>,N.B" }));
    }

    [Test]
    public void Parse_MatchWithoutClassName_OnePointWithNullClassName()
    {
        const string xml = @"<extension><instrumentation>
            <tracerFactory name=""F"">
              <match assemblyName=""A"">
                <exactMethodMatcher methodName=""M"" parameters="""" />
              </match>
            </tracerFactory>
          </instrumentation></extension>";

        var result = InstrumentationXmlParser.Parse(xml);

        Assert.That(result.Points, Has.Count.EqualTo(1));
        Assert.That(result.Points[0].ClassName, Is.Null);
    }

    [Test]
    public void Parse_DisabledFactoryWithCommaClassNames_BothClassPointsDisabled()
    {
        var result = InstrumentationXmlParser.Parse(FactoryXml(@"enabled=""false""", "").Replace(@"className=""B""", @"className=""B,C"""));

        Assert.That(result.Points.Select(p => p.ClassName).Distinct(), Is.EquivalentTo(new[] { "B", "C" }));
        Assert.That(result.Points.Select(p => p.Enabled), Is.All.False);
    }
}
