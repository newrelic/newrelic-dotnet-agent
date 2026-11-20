// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.OtelDistro.Generators.Tests;

[TestFixture]
public class OtelInstrumentationMapTests
{
    // Every member name of OTel v1.18.0's TracerInstrumentation enum (src/OpenTelemetry.AutoInstrumentation/Configurations/
    // TracerInstrumentation.cs), upper-cased as the OTEL_DOTNET_AUTO_TRACES_<ID>_INSTRUMENTATION_ENABLED key spells it.
    private static readonly string[] AllOtelTraceIds =
    {
        "HTTPCLIENT", "ASPNET", "SQLCLIENT", "GRAPHQL", "MONGODB", "NPGSQL", "MYSQLDATA", "STACKEXCHANGEREDIS", "GRPCNETCLIENT",
        "WCFSERVICE", "MASSTRANSIT", "NSERVICEBUS", "ELASTICSEARCH", "QUARTZ", "ENTITYFRAMEWORKCORE", "ASPNETCORE", "WCFCLIENT",
        "MYSQLCONNECTOR", "AZURE", "ELASTICTRANSPORT", "KAFKA", "ORACLEMDA", "RABBITMQ", "WCFCORE", "ADONET", "SQLITE"
    };

    // Where NR has no wrapper, OTel's instrumentation is the only coverage, so the map must never turn it off. AZURE is
    // here too: its row only records that it is never disabled (it also gates the Azure.Experimental.EnableActivitySource switch).
    private static readonly string[] KeepOnIds =
    {
        "ELASTICSEARCH", "ELASTICTRANSPORT", "ENTITYFRAMEWORKCORE", "GRAPHQL", "QUARTZ", "SQLITE", "GRPCNETCLIENT", "WCFCORE", "AZURE"
    };

    // Logs IDs (distro log forwarding rides ILOGGER) and the metrics-only IDs. Metrics IDs that share a name with a trace ID
    // (ASPNET, HTTPCLIENT, SQLCLIENT, ...) cannot be checked here: the arrays carry only names, and which signal they apply
    // to is decided by the key Core builds from them.
    private static readonly string[] LogsAndMetricsIds =
    {
        "ILOGGER", "LOG4NET", "NLOG", "NETRUNTIME", "PROCESS"
    };

    private static IEnumerable<TestCaseData> RowCases()
    {
        // BLOCK set: OTel's bytecode takes the same method NR targets.
        yield return Row("Confluent.Kafka", new[] { "KAFKA" }, new string[0]);
        yield return Row("Oracle.ManagedDataAccess", new[] { "ADONET", "ORACLEMDA" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet" });
        yield return Row("RabbitMQ.Client", new[] { "RABBITMQ" }, new[] { "OpenTelemetry.AutoInstrumentation.RabbitMq" });
        yield return Row("System.ServiceModel", new[] { "WCFCLIENT", "WCFSERVICE" }, new string[0]);

        // Duplicate set: both sides produce telemetry or propagation headers.
        yield return Row("StackExchange.Redis", new[] { "STACKEXCHANGEREDIS" }, new string[0]);
        yield return Row("StackExchange.Redis.StrongName", new[] { "STACKEXCHANGEREDIS" }, new string[0]);
        yield return Row("NServiceBus.Core", new[] { "NSERVICEBUS" }, new string[0]);
        yield return Row("MongoDB.Driver", new[] { "MONGODB" }, new string[0]);
        yield return Row("MongoDB.Driver.Core", new[] { "MONGODB" }, new string[0]);
        yield return Row("Npgsql", new[] { "ADONET", "NPGSQL" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet" });
        yield return Row("MySqlConnector", new[] { "ADONET", "MYSQLCONNECTOR" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet" });
        yield return Row("MySql.Data", new[] { "ADONET", "MYSQLDATA" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet" });
        yield return Row("Microsoft.Data.SqlClient", new[] { "ADONET", "SQLCLIENT" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet", "OpenTelemetry.Instrumentation.SqlClient" });
        yield return Row("System.Data.SqlClient", new[] { "ADONET", "SQLCLIENT" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet", "OpenTelemetry.Instrumentation.SqlClient" });
        yield return Row("System.Data", new[] { "ADONET", "SQLCLIENT" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet", "OpenTelemetry.Instrumentation.SqlClient" });
        yield return Row("System.Web", new[] { "ASPNET" }, new string[0]);

        // Other ADO.NET providers: only ADONET (OTel's Derived DbCommand definitions cover them all).
        foreach (var assembly in new[] { "Oracle.DataAccess", "System.Data.OracleClient", "System.Data.Odbc", "IBM.Data.DB2", "Devart.Data.MySql", "Devart.Data.Oracle" })
        {
            yield return Row(assembly, new[] { "ADONET" }, new[] { "OpenTelemetry.AutoInstrumentation.AdoNet" });
        }

        // Conditional set: disabled only because the matching NR XML is in the table.
        yield return Row("Microsoft.AspNetCore.Hosting", new[] { "ASPNETCORE" }, new string[0]);
        yield return Row("Microsoft.AspNetCore.Mvc.Core", new[] { "ASPNETCORE" }, new string[0]);
        yield return Row("MassTransit", new[] { "MASSTRANSIT" }, new string[0]);
    }

    private static TestCaseData Row(string assembly, string[] disable, string[] exclude) =>
        new TestCaseData(assembly, disable, exclude).SetName("Resolve_" + assembly.Replace('.', '_') + "_DisablesTheRowsIdsAndAddsItsSources");

    [TestCaseSource(nameof(RowCases))]
    public void Resolve_MapRow_DisablesTheRowsIdsAndAddsItsSources(string assembly, string[] expectedDisable, string[] expectedExclude)
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { assembly });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.EqualTo(expectedDisable));
            Assert.That(excludeSources, Is.EqualTo(expectedExclude));
        });
    }

    [TestCase("System.Net.Http")]
    [TestCase("System")]
    public void Resolve_OnlyOneOfTheHttpClientAndHttpWebRequestTargets_DoesNotDisableHttpClient(string assembly)
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { assembly });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.Empty);
            Assert.That(excludeSources, Is.Empty);
        });
    }

    [Test]
    public void Resolve_BothHttpClientAndHttpWebRequestTargets_DisablesHttpClientAndExcludesItsSources()
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { "System", "System.Net.Http" });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.EqualTo(new[] { "HTTPCLIENT" }));
            Assert.That(excludeSources, Is.EqualTo(new[] { "OpenTelemetry.Instrumentation.Http.HttpClient", "OpenTelemetry.Instrumentation.Http.HttpWebRequest" }));
        });
    }

    [Test]
    public void Resolve_HttpClientTargetAlone_DisablesHttpClientOnNetCoreOnly()
    {
        var netCore = OtelInstrumentationMap.Resolve(new[] { "System.Net.Http" }, DistroTargetRuntime.NetCore);
        var framework = OtelInstrumentationMap.Resolve(new[] { "System.Net.Http" }, DistroTargetRuntime.NetFramework);
        var unspecified = OtelInstrumentationMap.Resolve(new[] { "System.Net.Http" });

        Assert.Multiple(() =>
        {
            Assert.That(netCore.disableIds, Is.EqualTo(new[] { "HTTPCLIENT" }));
            Assert.That(netCore.excludeSources, Is.EqualTo(new[] { "OpenTelemetry.Instrumentation.Http.HttpClient", "OpenTelemetry.Instrumentation.Http.HttpWebRequest" }));
            Assert.That(framework.disableIds, Is.Empty);
            Assert.That(framework.excludeSources, Is.Empty);
            Assert.That(unspecified.disableIds, Is.Empty);
        });
    }

    [Test]
    public void Resolve_HttpWebRequestTargetAlone_KeepsHttpClientOnBothRuntimes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OtelInstrumentationMap.Resolve(new[] { "System" }, DistroTargetRuntime.NetCore).disableIds, Is.Empty);
            Assert.That(OtelInstrumentationMap.Resolve(new[] { "System" }, DistroTargetRuntime.NetFramework).disableIds, Is.Empty);
        });
    }

    [Test]
    public void Resolve_BothHttpTargets_DisablesHttpClientOnBothRuntimes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OtelInstrumentationMap.Resolve(new[] { "System", "System.Net.Http" }, DistroTargetRuntime.NetCore).disableIds, Is.EqualTo(new[] { "HTTPCLIENT" }));
            Assert.That(OtelInstrumentationMap.Resolve(new[] { "System", "System.Net.Http" }, DistroTargetRuntime.NetFramework).disableIds, Is.EqualTo(new[] { "HTTPCLIENT" }));
        });
    }

    [Test]
    public void Rows_OnlyHttpClientHasADifferentNetCoreTrigger()
    {
        var differing = OtelInstrumentationMap.Rows
            .Where(r => !r.NetCoreRequiredGroups.Select(g => string.Join(",", g)).SequenceEqual(r.RequiredGroups.Select(g => string.Join(",", g))))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(differing.SelectMany(r => r.OtelIds), Is.EqualTo(new[] { "HTTPCLIENT" }));
            Assert.That(differing.Single().NetCoreRequiredGroups, Is.EqualTo(new[] { new[] { "System.Net.Http" } }));
        });
    }

    [Test]
    public void Resolve_EveryMapAssembly_SameResultOnBothRuntimesExceptHttpClient()
    {
        // Every other ID keeps its behaviour on .NET: for each single assembly and for all of them at once.
        var everyAssembly = OtelInstrumentationMap.Rows.SelectMany(r => r.NrTargetAssemblies).Distinct().ToList();

        Assert.Multiple(() =>
        {
            foreach (var assembly in everyAssembly.Where(a => a != "System.Net.Http"))
            {
                AssertSameOnBothRuntimes(new[] { assembly }, assembly);
            }

            AssertSameOnBothRuntimes(everyAssembly, "every assembly");
        });
    }

    private static void AssertSameOnBothRuntimes(IReadOnlyList<string> assemblies, string label)
    {
        var netCore = OtelInstrumentationMap.Resolve(assemblies, DistroTargetRuntime.NetCore);
        var framework = OtelInstrumentationMap.Resolve(assemblies, DistroTargetRuntime.NetFramework);
        Assert.That(netCore.disableIds, Is.EqualTo(framework.disableIds), label);
        Assert.That(netCore.excludeSources, Is.EqualTo(framework.excludeSources), label);
    }

    [Test]
    public void Rows_IsTriggeredBy_DefaultOverloadUsesNetFrameworkGroups()
    {
        var row = OtelInstrumentationMap.Rows.Single(r => r.OtelIds.Contains("HTTPCLIENT"));
        var present = new HashSet<string> { "System.Net.Http" };

        Assert.Multiple(() =>
        {
            Assert.That(row.IsTriggeredBy(present), Is.False);
            Assert.That(row.IsTriggeredBy(present, DistroTargetRuntime.NetFramework), Is.False);
            Assert.That(row.IsTriggeredBy(present, DistroTargetRuntime.NetCore), Is.True);
        });
    }

    [Test]
    public void Rows_HttpClient_IsAllOfWithANote()
    {
        var row = OtelInstrumentationMap.Rows.Single(r => r.OtelIds.Contains("HTTPCLIENT"));

        Assert.Multiple(() =>
        {
            Assert.That(row.RequiredGroups, Has.Count.EqualTo(2));
            Assert.That(row.NrTargetAssemblies, Is.EquivalentTo(new[] { "System.Net.Http", "System" }));
            Assert.That(row.VersionExceptionNote, Does.Contain("HttpWebRequest"));
        });
    }

    [Test]
    public void Resolve_AzureAssemblies_NeverDisableAzure()
    {
        var azureAssemblies = new[] { "Azure.Messaging.ServiceBus", "Microsoft.Azure.Cosmos.Client", "Microsoft.Azure.Functions.Worker.Core" };
        var row = OtelInstrumentationMap.Rows.Single(r => r.NeverDisabledOtelIds.Contains("AZURE"));
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(azureAssemblies);
        var (allDisableIds, _) = OtelInstrumentationMap.Resolve(OtelInstrumentationMap.Rows.SelectMany(r => r.NrTargetAssemblies).Distinct());

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.Empty);
            Assert.That(excludeSources, Is.Empty);
            Assert.That(allDisableIds, Does.Not.Contain("AZURE"));
            Assert.That(row.OtelIds, Is.Empty);
            Assert.That(row.VersionExceptionNote, Does.Contain("Azure.Experimental.EnableActivitySource"));
        });
    }

    [TestCase("Npgsql", "4.1")]
    [TestCase("MySql.Data", "Firebird")]
    public void Rows_NpgsqlAndAdoNet_RecordTheirCaveats(string assembly, string expectedText)
    {
        var notes = OtelInstrumentationMap.Rows.Where(r => r.NrTargetAssemblies.Contains(assembly)).Select(r => r.VersionExceptionNote);

        Assert.That(notes, Has.Some.Contain(expectedText));
    }

    [Test]
    public void Resolve_MySqlData_DisablesAdoNetAndMySqlDataAndExcludesTheAdoNetSource()
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { "MySql.Data" });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.EqualTo(new[] { "ADONET", "MYSQLDATA" }));
            Assert.That(excludeSources, Is.EqualTo(new[] { "OpenTelemetry.AutoInstrumentation.AdoNet" }));
        });
    }

    [Test]
    public void Resolve_MicrosoftDataSqlClient_DisablesAdoNetAndSqlClientAndExcludesBothSources()
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { "Microsoft.Data.SqlClient" });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.EqualTo(new[] { "ADONET", "SQLCLIENT" }));
            Assert.That(excludeSources, Is.EqualTo(new[] { "OpenTelemetry.AutoInstrumentation.AdoNet", "OpenTelemetry.Instrumentation.SqlClient" }));
        });
    }

    [Test]
    public void Resolve_NoAspNetCoreAssembly_NeverDisablesAspNetCore()
    {
        // ASP.NET Core stays on in OTel unless NR's own ASP.NET Core XML is in the table: with OTel off and nothing of
        // NR's on, web transactions would be lost.
        var (disableIds, _) = OtelInstrumentationMap.Resolve(new[] { "System.Net.Http", "MySql.Data", "Microsoft.Data.SqlClient", "Hangfire.Core", "Couchbase.NetClient", "System.Web" });

        Assert.That(disableIds, Does.Not.Contain("ASPNETCORE"));
    }

    [Test]
    public void Resolve_RabbitMq_RecordsTheVersionExceptionAndKeepsTheNativeSources()
    {
        var row = OtelInstrumentationMap.Rows.Single(r => r.NrTargetAssemblies.Contains("RabbitMQ.Client"));
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { "RabbitMQ.Client" });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Does.Contain("RABBITMQ"));
            Assert.That(row.VersionExceptionNote, Is.Not.Empty);
            Assert.That(row.VersionExceptionNote, Does.Contain("6.8.1"));
            // RabbitMQ.Client 7+ emits these natively; NR's XML defers there, so its listener must keep them.
            Assert.That(excludeSources, Does.Not.Contain("RabbitMQ.Client.Publisher"));
            Assert.That(excludeSources, Does.Not.Contain("RabbitMQ.Client.Subscriber"));
        });
    }

    [Test]
    public void Rows_EveryRow_NamesAssembliesAndIdsAndHasANonNullNote()
    {
        Assert.That(OtelInstrumentationMap.Rows, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var row in OtelInstrumentationMap.Rows)
            {
                Assert.That(row.NrTargetAssemblies, Is.Not.Empty);
                Assert.That(row.OtelIds.Concat(row.NeverDisabledOtelIds), Is.Not.Empty, string.Join(",", row.NrTargetAssemblies));
                Assert.That(row.RequiredGroups, Is.Not.Empty);
                Assert.That(row.RequiredGroups, Has.All.Not.Empty);
                Assert.That(row.ExcludeSources, Is.Not.Null);
                Assert.That(row.VersionExceptionNote, Is.Not.Null);
            }
        });
    }

    [Test]
    public void Rows_EveryOtelId_IsARealTraceIdThatIsNeverKeepOn()
    {
        var ids = OtelInstrumentationMap.Rows.SelectMany(r => r.OtelIds).Distinct().ToList();
        var recorded = OtelInstrumentationMap.Rows.SelectMany(r => r.NeverDisabledOtelIds).Distinct().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(recorded.Except(AllOtelTraceIds), Is.Empty);
            Assert.That(ids.Except(AllOtelTraceIds), Is.Empty, "an ID the map names that OTel v1.18.0 does not have");
            Assert.That(ids.Intersect(KeepOnIds), Is.Empty);
            Assert.That(ids.Intersect(LogsAndMetricsIds), Is.Empty);
        });
    }

    [Test]
    public void Resolve_EveryAssemblyTheMapKnows_NeverDisablesAKeepOnLogsOrMetricsId()
    {
        var everyAssembly = OtelInstrumentationMap.Rows.SelectMany(r => r.NrTargetAssemblies).Concat(new[] { "Elastic.Transport", "Elasticsearch.Net", "Quartz", "Microsoft.EntityFrameworkCore", "log4net", "NLog", "Microsoft.Extensions.Logging" }).ToList();

        var (disableIds, _) = OtelInstrumentationMap.Resolve(everyAssembly);

        Assert.Multiple(() =>
        {
            Assert.That(disableIds.Intersect(KeepOnIds), Is.Empty);
            Assert.That(disableIds.Intersect(LogsAndMetricsIds), Is.Empty);
            Assert.That(disableIds.Except(AllOtelTraceIds), Is.Empty);
        });
    }

    [TestCase("Elastic.Transport")]
    [TestCase("Elasticsearch.Net")]
    [TestCase("OpenSearch.Net")]
    [TestCase("log4net")]
    [TestCase("NLog")]
    [TestCase("Microsoft.Extensions.Logging")]
    [TestCase("Serilog")]
    [TestCase("Hangfire.Core")]
    [TestCase("Couchbase.NetClient")]
    [TestCase("NotAnAssembly")]
    public void Resolve_AssemblyWithNoMapRow_ChangesNothing(string assembly)
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[] { assembly });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.Empty);
            Assert.That(excludeSources, Is.Empty);
        });
    }

    [Test]
    public void Resolve_NoAssemblies_ReturnsEmpty()
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new string[0]);

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.Empty);
            Assert.That(excludeSources, Is.Empty);
        });
    }

    [Test]
    public void Resolve_NullAssemblies_Throws()
    {
        Assert.That(() => OtelInstrumentationMap.Resolve(null), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void Resolve_AssemblyNames_AreMatchedExactly()
    {
        var (disableIds, _) = OtelInstrumentationMap.Resolve(new[] { "mysql.data", "MySql.Data.Extra", null, "" });

        Assert.That(disableIds, Is.Empty);
    }

    [Test]
    public void Resolve_ManyRowsSharingIds_IsSortedAndDistinct()
    {
        var (disableIds, excludeSources) = OtelInstrumentationMap.Resolve(new[]
        {
            "System.Web", "Npgsql", "MySql.Data", "Microsoft.Data.SqlClient", "System.Data", "Oracle.ManagedDataAccess", "Npgsql", "Confluent.Kafka"
        });

        Assert.Multiple(() =>
        {
            Assert.That(disableIds, Is.EqualTo(new[] { "ADONET", "ASPNET", "KAFKA", "MYSQLDATA", "NPGSQL", "ORACLEMDA", "SQLCLIENT" }));
            Assert.That(excludeSources, Is.EqualTo(new[] { "OpenTelemetry.AutoInstrumentation.AdoNet", "OpenTelemetry.Instrumentation.SqlClient" }));
            Assert.That(disableIds, Is.Ordered.Using<string>(StringComparer.Ordinal));
            Assert.That(excludeSources, Is.Ordered.Using<string>(StringComparer.Ordinal));
        });
    }

    [Test]
    public void Resolve_SameInputInAnyOrder_GivesTheSameOutput()
    {
        var assemblies = new[] { "System.Net.Http", "RabbitMQ.Client", "Npgsql", "System.ServiceModel" };

        var forward = OtelInstrumentationMap.Resolve(assemblies);
        var backward = OtelInstrumentationMap.Resolve(assemblies.Reverse());

        Assert.Multiple(() =>
        {
            Assert.That(backward.disableIds, Is.EqualTo(forward.disableIds));
            Assert.That(backward.excludeSources, Is.EqualTo(forward.excludeSources));
        });
    }

    [Test]
    public void OtelVersion_IsTheVersionTheMapWasBuiltFor()
    {
        Assert.That(OtelInstrumentationMap.OtelVersion, Is.EqualTo("1.18.0"));
    }
}
