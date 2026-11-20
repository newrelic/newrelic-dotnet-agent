// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.OtelDistro.Generators;

/// <summary>
/// The runtime the distro plugin is built for, which decides a map row's trigger where OTel's coverage differs by runtime.
/// <see cref="NetFramework"/> is the default: an unknown target framework gets the .NET Framework (stricter) rules.
/// </summary>
public enum DistroTargetRuntime
{
    NetFramework,
    NetCore,
}

/// <summary>
/// One row of <see cref="OtelInstrumentationMap"/>: when its trigger holds for the target assemblies of the NR
/// instrumentation matches in the definitions table, OTel's trace instrumentation <see cref="OtelIds"/> are disabled
/// (they would take the same methods first, or duplicate NR's telemetry) and <see cref="ExcludeSources"/> are added to the
/// ActivitySource default-exclude list. The trigger is a list of groups: every group must have at least one of its
/// assemblies present (any-of within a group, all-of across groups). Most rows have one group, and the same trigger on
/// every runtime; a row may give .NET (Core/5+) its own groups (<see cref="NetCoreRequiredGroups"/>).
/// </summary>
public sealed class OtelInstrumentationMapRow
{
    private readonly string[][] _requiredGroups;
    private readonly string[][] _netCoreRequiredGroups;

    /// <summary>Every <c>assemblyName</c> that takes part in the trigger (exact, case-sensitive), across all groups.</summary>
    public IReadOnlyList<string> NrTargetAssemblies { get; }

    /// <summary>
    /// The trigger groups on .NET Framework (and for an unknown runtime): the row fires only when each group has at least
    /// one assembly present. A single group is a plain any-of; several groups are an all-of (e.g. HTTPCLIENT on .NET
    /// Framework needs both the HttpClient and the HttpWebRequest XML).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> RequiredGroups { get; }

    /// <summary>The trigger groups on .NET (Core/5+); the same as <see cref="RequiredGroups"/> unless the row gives its own.</summary>
    public IReadOnlyList<IReadOnlyList<string>> NetCoreRequiredGroups { get; }

    /// <summary>
    /// Upper-case OTel TracerInstrumentation enum names to disable, as spelled in
    /// <c>OTEL_DOTNET_AUTO_TRACES_{ID}_INSTRUMENTATION_ENABLED</c>. Empty for a row that only records an ID that is never
    /// disabled (see <see cref="NeverDisabledOtelIds"/>).
    /// </summary>
    public IReadOnlyList<string> OtelIds { get; }

    /// <summary>OTel IDs the row records as deliberately never disabled; <see cref="OtelInstrumentationMap.Resolve(IEnumerable{string}, DistroTargetRuntime)"/> never emits them.</summary>
    public IReadOnlyList<string> NeverDisabledOtelIds { get; }

    /// <summary>
    /// ActivitySource names to add to the default-exclude list. Only names the hybrid agent's default-exclude list
    /// (DefaultConfiguration.OpenTelemetryTracingDefaultExcludedActivitySources) does not already hold; the union with
    /// that list happens in Core.
    /// </summary>
    public IReadOnlyList<string> ExcludeSources { get; }

    /// <summary>
    /// The row's caveat: a range of the library's versions NR's XML does not cover, a wider effect than NR's XML, or why an
    /// ID is never disabled or needs more than one NR target. Empty when the row has none.
    /// </summary>
    public string VersionExceptionNote { get; }

    public OtelInstrumentationMapRow(string[][] requiredGroups, string[] otelIds, string[] neverDisabledOtelIds, string[] excludeSources, string versionExceptionNote)
        : this(requiredGroups, requiredGroups, otelIds, neverDisabledOtelIds, excludeSources, versionExceptionNote)
    {
    }

    /// <summary>A row whose trigger on .NET (Core/5+), <paramref name="netCoreRequiredGroups"/>, differs from the one on .NET Framework.</summary>
    public OtelInstrumentationMapRow(string[][] requiredGroups, string[][] netCoreRequiredGroups, string[] otelIds, string[] neverDisabledOtelIds, string[] excludeSources, string versionExceptionNote)
    {
        _requiredGroups = requiredGroups;
        _netCoreRequiredGroups = netCoreRequiredGroups;
        RequiredGroups = AsReadOnly(requiredGroups);
        NetCoreRequiredGroups = AsReadOnly(netCoreRequiredGroups);
        NrTargetAssemblies = Array.AsReadOnly(requiredGroups.Concat(netCoreRequiredGroups).SelectMany(group => group).Distinct(StringComparer.Ordinal).ToArray());
        OtelIds = Array.AsReadOnly(otelIds);
        NeverDisabledOtelIds = Array.AsReadOnly(neverDisabledOtelIds);
        ExcludeSources = Array.AsReadOnly(excludeSources);
        VersionExceptionNote = versionExceptionNote;
    }

    /// <summary>True when every .NET Framework trigger group has at least one assembly in <paramref name="present"/>.</summary>
    public bool IsTriggeredBy(ISet<string> present) => IsTriggeredBy(present, DistroTargetRuntime.NetFramework);

    /// <summary>True when every trigger group for <paramref name="runtime"/> has at least one assembly in <paramref name="present"/>.</summary>
    public bool IsTriggeredBy(ISet<string> present, DistroTargetRuntime runtime) =>
        (runtime == DistroTargetRuntime.NetCore ? _netCoreRequiredGroups : _requiredGroups).All(group => group.Any(present.Contains));

    private static IReadOnlyList<IReadOnlyList<string>> AsReadOnly(string[][] groups) =>
        Array.AsReadOnly(groups.Select(group => (IReadOnlyList<string>)Array.AsReadOnly(group)).ToArray());
}

/// <summary>
/// The one place that says which OTel built-in trace instrumentation to turn off, and which ActivitySources to stop
/// listening to, for the NR instrumentation XML that is in the distro definitions table. Keyed by the target assembly
/// names of the XML's default-on matches (a default-off point, from a factory with enabled="false", never triggers a
/// row; the generator's NRDG011 reports a library left to OTel that way).
///
/// Built from OTel v1.18.0's instrumentation inventory (its docs/config.md instrumentation tables and the
/// TracerInstrumentation enum) against the shipped NR wrapper XML (src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/*/Instrumentation.xml).
/// Source names are from OTel's EnvironmentConfigurationTracerHelper.cs and Instrumentations/*Instrumentation.cs.
///
/// Never disabled, whatever the XML: AZURE (see its row), and, with no row at all: ELASTICSEARCH, ELASTICTRANSPORT, ENTITYFRAMEWORKCORE, GRAPHQL, QUARTZ, SQLITE,
/// GRPCNETCLIENT, WCFCORE (OTel is the only coverage), every logs ID (distro log forwarding rides ILOGGER) and every
/// metrics ID (exported only by OTel's own SDK, whose exporter is none).
/// </summary>
public static class OtelInstrumentationMap
{
    /// <summary>The OTel auto-instrumentation version the map was built for.</summary>
    public const string OtelVersion = "1.18.0";

    private const string AdoNetSource = "OpenTelemetry.AutoInstrumentation.AdoNet";
    private const string SqlClientSource = "OpenTelemetry.Instrumentation.SqlClient";

    private const string RabbitMqVersionException =
        "NR's RabbitMq XML covers RabbitMQ.Client up to 6.8.1 (maxVersion) and defers above it. RabbitMQ.Client 7+ emits "
        + "library-native ActivitySources (RabbitMQ.Client.Publisher, RabbitMQ.Client.Subscriber) that NR's own listener sees "
        + "without OTel's RABBITMQ ID, so the ID is disabled and those two sources are deliberately not excluded.";

    private const string AdoNetNote =
        "Disabling ADONET turns off OTel's DbCommand instrumentation for every ADO.NET provider, including those NR's Sql XML "
        + "does not instrument (e.g. Firebird); NR has no wrapper there, so those providers lose OTel's other_sql Activities.";

    private const string NpgsqlNote =
        "NR's Sql XML covers Npgsql from 4.1 (minVersion) on; Npgsql below 4.1 has no NR instrumentation while ADONET and "
        + "NPGSQL are disabled.";

    private const string HttpClientNote =
        "OTel's HTTPCLIENT covers both HttpClient and, on .NET Framework, HttpWebRequest: its .NET Framework build instruments "
        + "at the HttpWebRequest layer only (source OpenTelemetry.Instrumentation.Http.HttpWebRequest). On .NET Framework it is "
        + "disabled only when BOTH the HttpClient XML (System.Net.Http) and the HttpWebRequest XML (System) are in the table; "
        + "with either alone, disabling it would leave the other's outbound HTTP calls uncovered. On .NET (Core/5+) the "
        + "HttpClient XML alone disables it: HttpWebRequest runs on HttpClient there, so NR's SocketsHttpHandler wrapper covers "
        + "both, and left on, OTel's HttpClient instrumentation replaces NR's outbound traceparent with its own.";

    private const string AzureNote =
        "AZURE also gates OTel's AppContext switch Azure.Experimental.EnableActivitySource, which NR does not set; disabling it "
        + "would stop unwrapped Azure SDKs from emitting Activities to NR's listener. NR-covered Azure sources stay excluded via "
        + "the hybrid default-exclude list.";

    private static readonly string[] None = new string[0];

    // Source names listed: the OTel sources of the row's IDs that the hybrid default-exclude list does not hold. Where
    // the list already holds them (connector-net, MySqlConnector, Npgsql, Microsoft.Data.SqlClient, System.Data*,
    // OpenTelemetry.AutoInstrumentation.Kafka/MongoDB, OpenTelemetry.Instrumentation.AspNet/StackExchangeRedis/Wcf,
    // Oracle.ManagedDataAccess[.Core], MassTransit, NServiceBus.Core, MongoDB.Driver, Microsoft.AspNetCore*, System.Net.Http)
    // the row lists nothing.
    //
    // Caution: the keys "System" (HttpWebRequest XML) and "System.Data" (Sql XML) are generic assembly names. Any future
    // wrapper whose XML targets those assemblies for something else will trigger the HTTPCLIENT (with System.Net.Http) and
    // ADONET/SQLCLIENT rows below; revisit the keys when adding one.
    private static readonly OtelInstrumentationMapRow[] AllRows =
    {
        // BLOCK: OTel's CallTarget definitions would take the same methods NR targets (first registered wins).

        // ADONET rewrites every DbCommand override (Derived), so it blocks every provider NR's Sql XML instruments.
        AnyOf(
            new[]
            {
                "System.Data", "System.Data.SqlClient", "Microsoft.Data.SqlClient", "MySql.Data", "MySqlConnector", "Npgsql",
                "Oracle.ManagedDataAccess", "Oracle.DataAccess", "System.Data.OracleClient", "System.Data.Odbc", "IBM.Data.DB2",
                "Devart.Data.MySql", "Devart.Data.Oracle"
            },
            new[] { "ADONET" },
            new[] { AdoNetSource },
            AdoNetNote),
        AnyOf(new[] { "Confluent.Kafka" }, new[] { "KAFKA" }, None, string.Empty),
        AnyOf(new[] { "RabbitMQ.Client" }, new[] { "RABBITMQ" }, new[] { "OpenTelemetry.AutoInstrumentation.RabbitMq" }, RabbitMqVersionException),
        AnyOf(new[] { "Oracle.ManagedDataAccess" }, new[] { "ORACLEMDA" }, None, string.Empty),
        // WCFCLIENT blocks ChannelFactory.InitializeEndpoint on .NET Framework (System.ServiceModel); the map is not
        // framework-aware, it follows the XML the generator was given for the target framework.
        AnyOf(new[] { "System.ServiceModel" }, new[] { "WCFCLIENT", "WCFSERVICE" }, None, string.Empty),

        // DUPLICATE: no method collision, but both sides produce telemetry, or both inject propagation headers.
        AnyOf(new[] { "System.Data", "System.Data.SqlClient", "Microsoft.Data.SqlClient" }, new[] { "SQLCLIENT" }, new[] { SqlClientSource }, string.Empty),
        AnyOf(new[] { "StackExchange.Redis", "StackExchange.Redis.StrongName" }, new[] { "STACKEXCHANGEREDIS" }, None, string.Empty),
        AnyOf(new[] { "NServiceBus.Core" }, new[] { "NSERVICEBUS" }, None, string.Empty),
        AnyOf(new[] { "MongoDB.Driver", "MongoDB.Driver.Core" }, new[] { "MONGODB" }, None, string.Empty),
        AnyOf(new[] { "Npgsql" }, new[] { "NPGSQL" }, None, NpgsqlNote),
        AnyOf(new[] { "MySql.Data" }, new[] { "MYSQLDATA" }, None, string.Empty),
        AnyOf(new[] { "MySqlConnector" }, new[] { "MYSQLCONNECTOR" }, None, string.Empty),

        // CONDITIONAL: only because NR's XML for the library is in the table, so the library is never left uncovered.
        AnyOf(new[] { "System.Web" }, new[] { "ASPNET" }, None, string.Empty),
        AnyOf(new[] { "Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Mvc.Core" }, new[] { "ASPNETCORE" }, None, string.Empty),
        // .NET Framework: all-of HttpClient (System.Net.Http) and HttpWebRequest (System). .NET: HttpClient alone. The
        // contrib sources are not in the hybrid list.
        new OtelInstrumentationMapRow(
            new[] { new[] { "System.Net.Http" }, new[] { "System" } },
            new[] { new[] { "System.Net.Http" } },
            new[] { "HTTPCLIENT" },
            None,
            new[] { "OpenTelemetry.Instrumentation.Http.HttpClient", "OpenTelemetry.Instrumentation.Http.HttpWebRequest" },
            HttpClientNote),
        AnyOf(new[] { "MassTransit" }, new[] { "MASSTRANSIT" }, None, string.Empty),
        // Never disabled (kept in the map so the decision is visible): AZURE is OTel's "Azure.*" SDK sources plus an
        // AppContext switch NR does not set, so turning it off loses Azure SDK Activities NR has no wrapper for.
        new OtelInstrumentationMapRow(
            new[] { new[] { "Azure.Messaging.ServiceBus", "Microsoft.Azure.Cosmos.Client" } },
            None,
            new[] { "AZURE" },
            None,
            AzureNote),
    };

    // A row triggered when any one of the assemblies is present.
    private static OtelInstrumentationMapRow AnyOf(string[] assemblies, string[] otelIds, string[] excludeSources, string note) =>
        new OtelInstrumentationMapRow(new[] { assemblies }, otelIds, None, excludeSources, note);

    /// <summary>Every row of the map.</summary>
    public static IReadOnlyList<OtelInstrumentationMapRow> Rows { get; } = Array.AsReadOnly(AllRows);

    /// <summary>
    /// The OTel trace instrumentation IDs to disable and the ActivitySource names to add to the exclude list for NR
    /// matches that target <paramref name="nrTargetAssemblies"/>, with the .NET Framework triggers. Both lists are
    /// distinct and sorted (ordinal).
    /// </summary>
    public static (IReadOnlyList<string> disableIds, IReadOnlyList<string> excludeSources) Resolve(IEnumerable<string> nrTargetAssemblies) =>
        Resolve(nrTargetAssemblies, DistroTargetRuntime.NetFramework);

    /// <summary>
    /// The OTel trace instrumentation IDs to disable and the ActivitySource names to add to the exclude list for NR
    /// matches that target <paramref name="nrTargetAssemblies"/>, with the triggers for <paramref name="runtime"/>. Both
    /// lists are distinct and sorted (ordinal).
    /// </summary>
    public static (IReadOnlyList<string> disableIds, IReadOnlyList<string> excludeSources) Resolve(IEnumerable<string> nrTargetAssemblies, DistroTargetRuntime runtime)
    {
        if (nrTargetAssemblies == null)
        {
            throw new ArgumentNullException(nameof(nrTargetAssemblies));
        }

        var present = new HashSet<string>(nrTargetAssemblies.Where(assembly => assembly != null), StringComparer.Ordinal);
        var triggered = AllRows.Where(row => row.IsTriggeredBy(present, runtime)).ToList();

        var disableIds = triggered.SelectMany(row => row.OtelIds).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var excludeSources = triggered.SelectMany(row => row.ExcludeSources).Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal).ToList();
        return (disableIds, excludeSources);
    }
}
