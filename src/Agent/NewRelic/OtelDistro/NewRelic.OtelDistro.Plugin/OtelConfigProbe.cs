// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NewRelic.Agent.Core.OtelDistro;
using OpenTelemetry.AutoInstrumentation.CallTarget;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// The reflection adapter onto OTel auto-instrumentation internals (pinned to OTel 1.18.0), used from
/// <see cref="Plugin.Initialized"/>, after OTel has built its settings. Reads the effective settings into a
/// <see cref="DistroOtelConfigSnapshot"/> and the CallTarget sites of the enabled OTel instrumentations into
/// <see cref="OtelSite"/>s. Every decision is Core's; this only reads. Never throws: a part that cannot be read (an
/// OTel version whose internals differ) is left out and named, and the rest is still read (see <see cref="Probe"/>).
/// </summary>
internal static class OtelConfigProbe
{
    // OTel internals (src/OpenTelemetry.AutoInstrumentation, v1.18.0):
    // Instrumentation.cs:56-60 - internal static Lazy<TracerSettings|MetricSettings|LogSettings> properties.
    private const string InstrumentationTypeName = "OpenTelemetry.AutoInstrumentation.Instrumentation";

    // Configurations/Settings.cs:15 - private static readonly bool IsYamlConfigEnabled (env flag exactly "true").
    private const string SettingsTypeName = "OpenTelemetry.AutoInstrumentation.Configurations.Settings";
    private const string YamlEnabledFieldName = "IsYamlConfigEnabled";
    private const string YamlEnabledVariable = "OTEL_EXPERIMENTAL_FILE_BASED_CONFIGURATION_ENABLED";

    // Instrumentations/InstrumentMethodAttribute.cs:10-91 - internal, on the integration classes (and allowed on the assembly).
    private const string InstrumentMethodAttributeName = "OpenTelemetry.AutoInstrumentation.Instrumentations.InstrumentMethodAttribute";

    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Reads OTel's effective configuration and the CallTarget sites of its enabled instrumentations. Each part is read
    /// on its own, so drift in one part of OTel's internals still yields the rest, above all the file-based (YAML) flag.
    /// Unread parts are left null or missing (the site list empty) and named in <paramref name="failure"/> and in
    /// <see cref="DistroOtelConfigSnapshot.PartialDetectionFailure"/>, which Core reports as DetectionUnavailable.
    /// <paramref name="failure"/> is null when everything was read; the snapshot is null only on an unexpected failure.
    /// </summary>
    internal static DistroOtelConfigSnapshot Probe(out IReadOnlyList<OtelSite> otelSites, out string failure)
    {
        otelSites = new OtelSite[0];
        try
        {
            // A public OTel type of the managed assembly OTel loaded, the one the plugin is bound to.
            var otelAssembly = typeof(CallTargetState).Assembly;
            var failures = new List<string>();

            // First and on its own: the YAML flag is the headline detection and must survive drift anywhere else.
            var fileBasedConfigActive = ReadFileBasedConfigActive(otelAssembly, failures);

            SignalSettings tracer = null;
            SignalSettings metric = null;
            SignalSettings log = null;
            Type instrumentation = null;
            try
            {
                instrumentation = otelAssembly.GetType(InstrumentationTypeName, true);
            }
            catch (Exception ex)
            {
                failures.Add("signal settings: " + Describe(ex));
            }

            if (instrumentation != null)
            {
                tracer = TryReadSignal(instrumentation, "TracerSettings", "TracesEnabled", "TracesExporters", "Processors", failures);
                metric = TryReadSignal(instrumentation, "MetricSettings", "MetricsEnabled", "MetricExporters", "Readers", failures);
                log = TryReadSignal(instrumentation, "LogSettings", "LogsEnabled", "LogExporters", "Processors", failures);
            }

            var exporters = new Dictionary<string, string>();
            AddExporters(exporters, "Traces", tracer, fileBasedConfigActive, failures);
            AddExporters(exporters, "Metrics", metric, fileBasedConfigActive, failures);
            AddExporters(exporters, "Logs", log, fileBasedConfigActive, failures);

            try
            {
                otelSites = ReadOtelSites(otelAssembly, tracer, metric, log);
            }
            catch (Exception ex)
            {
                otelSites = new OtelSite[0];
                failures.Add("instrumentation sites: " + Describe(ex));
            }

            failure = failures.Count == 0 ? null : string.Join("; ", failures);
            return new DistroOtelConfigSnapshot
            {
                FileBasedConfigActive = fileBasedConfigActive,
                EnabledTraceInstrumentationIds = tracer?.EnabledIds,
                Exporters = exporters,
                PartialDetectionFailure = failure,
            };
        }
        catch (Exception ex)
        {
            otelSites = new OtelSite[0];
            failure = Describe(ex);
            return null;
        }
    }

    private static bool ReadFileBasedConfigActive(Assembly otelAssembly, List<string> failures)
    {
        try
        {
            var settingsType = otelAssembly.GetType(SettingsTypeName, true);
            var yamlField = settingsType.GetField(YamlEnabledFieldName, StaticMembers) ?? throw new MissingFieldException(SettingsTypeName, YamlEnabledFieldName);
            return (bool)yamlField.GetValue(null);
        }
        catch (Exception ex)
        {
            // Fall back to OTel's own rule (Settings.cs:15); the field only caches it, read before plugins load.
            failures.Add("file-based config flag (fell back to the environment variable): " + Describe(ex));
            try
            {
                return Environment.GetEnvironmentVariable(YamlEnabledVariable) == "true";
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private static SignalSettings TryReadSignal(Type instrumentation, string lazyProperty, string enabledProperty, string exportersProperty, string yamlPipelineProperty, List<string> failures)
    {
        try
        {
            return SignalSettings.Read(instrumentation, lazyProperty, enabledProperty, exportersProperty, yamlPipelineProperty);
        }
        catch (Exception ex)
        {
            failures.Add(lazyProperty + ": " + Describe(ex));
            return null;
        }
    }

    private static void AddExporters(Dictionary<string, string> exporters, string signalName, SignalSettings signal, bool fileBasedConfigActive, List<string> failures)
    {
        if (signal == null)
        {
            return;
        }

        try
        {
            exporters[signalName] = signal.DescribeExporters(fileBasedConfigActive);
        }
        catch (Exception ex)
        {
            failures.Add(signalName + " exporters: " + Describe(ex));
        }
    }

    private static string Describe(Exception ex)
    {
        return ex.GetType().Name + ": " + ex.Message;
    }

    /// <summary>
    /// The NR sites of the plugin's definitions table that are on (the effective set: the shipped defaults with the XML on
    /// disk applied at start-up, see Plugin.GetAllDefinitionsPayload, which runs first); empty when the table was rejected.
    /// Never throws.
    /// </summary>
    internal static IReadOnlyList<NrSite> ReadNrSites()
    {
        try
        {
            var sites = DistroSiteTable.Sites;
            if (sites == null)
            {
                return new NrSite[0];
            }

            var result = new List<NrSite>(sites.Count);
            for (var i = 0; i < sites.Count; i++)
            {
                if (sites.IsOn(i) && sites.TryGet(i, out var site))
                {
                    result.Add(NrSite.From(site));
                }
            }

            return result;
        }
        catch (Exception)
        {
            return new NrSite[0];
        }
    }

    private static IReadOnlyList<OtelSite> ReadOtelSites(Assembly otelAssembly, SignalSettings tracer, SignalSettings metric, SignalSettings log)
    {
        Type[] types;
        try
        {
            types = otelAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
        }

        var sites = new List<OtelSite>();
        AddSites(sites, otelAssembly.GetCustomAttributesData(), tracer, metric, log);
        foreach (var type in types)
        {
            AddSites(sites, type.GetCustomAttributesData(), tracer, metric, log);
        }

        return sites;
    }

    private static void AddSites(List<OtelSite> sites, IList<CustomAttributeData> attributes, SignalSettings tracer, SignalSettings metric, SignalSettings log)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName != InstrumentMethodAttributeName)
            {
                continue;
            }

            var arguments = ReadArguments(attribute);
            var integration = (string)Required(arguments, "integrationName").Value;

            // Gated as OTel gates its definitions (InstrumentationDefinitions.Shared.g.cs): the signal is enabled and its
            // instrumentation ID is in the signal's enabled list.
            SignalSettings signal;
            switch (EnumName(Required(arguments, "type")))
            {
                case "Trace":
                    signal = tracer;
                    break;
                case "Metric":
                    signal = metric;
                    break;
                case "Log":
                    signal = log;
                    break;
                default:
                    continue;
            }

            if (signal == null)
            {
                // That signal's settings could not be read (already reported); its sites are unknown.
                continue;
            }

            var id = signal.EnabledIds.FirstOrDefault(enabled => string.Equals(enabled, integration, StringComparison.OrdinalIgnoreCase));
            if (id == null)
            {
                continue;
            }

            // kind is optional (default Direct); the compiler writes the default into the metadata, so it is normally present.
            var hasKind = arguments.TryGetValue("kind", out var kind) && kind.Value != null;
            sites.Add(new OtelSite
            {
                Id = id,
                Integration = integration,
                Assembly = (string)Required(arguments, "assemblyName").Value,
                Type = (string)Required(arguments, "typeName").Value,
                Method = (string)Required(arguments, "methodName").Value,
                Signature = ReadStringArray(arguments.TryGetValue("parameterTypeNames", out var parameters) ? parameters.Value : null),
                MatchesDerivedOrInterface = hasKind && EnumName(kind) != "Direct",
            });
        }
    }

    // Constructor arguments by parameter name (so a reordered constructor still reads), plus any named arguments.
    private static Dictionary<string, CustomAttributeTypedArgument> ReadArguments(CustomAttributeData attribute)
    {
        var arguments = new Dictionary<string, CustomAttributeTypedArgument>(StringComparer.OrdinalIgnoreCase);
        var parameters = attribute.Constructor.GetParameters();
        for (var i = 0; i < parameters.Length && i < attribute.ConstructorArguments.Count; i++)
        {
            arguments[parameters[i].Name] = attribute.ConstructorArguments[i];
        }

        foreach (var named in attribute.NamedArguments)
        {
            arguments[named.MemberName] = named.TypedValue;
        }

        return arguments;
    }

    private static CustomAttributeTypedArgument Required(Dictionary<string, CustomAttributeTypedArgument> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value.Value == null)
        {
            throw new MissingMemberException(InstrumentMethodAttributeName, name);
        }

        return value;
    }

    private static string EnumName(CustomAttributeTypedArgument argument)
    {
        return argument.ArgumentType.IsEnum ? Enum.GetName(argument.ArgumentType, argument.Value) : Convert.ToString(argument.Value);
    }

    private static IReadOnlyList<string> ReadStringArray(object value)
    {
        if (value is IEnumerable<CustomAttributeTypedArgument> elements)
        {
            return elements.Select(e => (string)e.Value).ToArray();
        }

        return new string[0];
    }

    // Snake case of an OTel YAML exporter config property: OtlpHttp -> otlp_http, as in the YAML (YamlMember aliases).
    private static string ToYamlName(string propertyName)
    {
        var builder = new StringBuilder(propertyName.Length + 4);
        for (var i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];
            if (char.IsUpper(c) && i > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>One signal's settings object (TracerSettings, MetricSettings or LogSettings) and what is read from it.</summary>
    private sealed class SignalSettings
    {
        private readonly object _settings;
        private readonly string _exportersProperty;
        private readonly string _yamlPipelineProperty;

        public bool Enabled { get; }

        /// <summary>Upper-case instrumentation IDs (enum names) when the signal is enabled; empty when it is not.</summary>
        public IReadOnlyList<string> EnabledIds { get; }

        private SignalSettings(object settings, bool enabled, IReadOnlyList<string> enabledIds, string exportersProperty, string yamlPipelineProperty)
        {
            _settings = settings;
            Enabled = enabled;
            EnabledIds = enabledIds;
            _exportersProperty = exportersProperty;
            _yamlPipelineProperty = yamlPipelineProperty;
        }

        public static SignalSettings Read(Type instrumentation, string lazyProperty, string enabledProperty, string exportersProperty, string yamlPipelineProperty)
        {
            var lazy = GetProperty(instrumentation, null, lazyProperty, StaticMembers);
            var settings = GetProperty(lazy.GetType(), lazy, "Value", InstanceMembers);
            var enabled = (bool)GetProperty(settings.GetType(), settings, enabledProperty, InstanceMembers);
            var ids = (IEnumerable)GetProperty(settings.GetType(), settings, "EnabledInstrumentations", InstanceMembers);
            var enabledIds = enabled ? ids.Cast<object>().Select(id => id.ToString().ToUpperInvariant()).ToArray() : new string[0];

            // Touch both exporter members now, so a missing one fails the probe here rather than half-way through.
            GetPropertyInfo(settings.GetType(), exportersProperty, InstanceMembers);
            GetPropertyInfo(settings.GetType(), yamlPipelineProperty, InstanceMembers);

            return new SignalSettings(settings, enabled, enabledIds, exportersProperty, yamlPipelineProperty);
        }

        /// <summary>
        /// The effective exporters as a comma list (lower case), or <c>none</c> when there are none or the signal is off
        /// (OTel then builds no exporter for it). Env mode: the parsed exporter list (an unset OTEL_*_EXPORTER is OTel's
        /// default otlp, already resolved there). YAML mode: the exporter of each processor/reader the YAML declares.
        /// </summary>
        public string DescribeExporters(bool fileBasedConfigActive)
        {
            if (!Enabled)
            {
                return "none";
            }

            var names = new List<string>();
            if (fileBasedConfigActive)
            {
                // TracerSettings/LogSettings.Processors: ProcessorConfig { Batch | Simple { Exporter { OtlpHttp | OtlpGrpc | Zipkin | Console } } };
                // MetricSettings.Readers: MetricReaderConfig { Periodic | Pull { Exporter { OtlpHttp | OtlpGrpc | Console | Prometheus } } }.
                if (GetProperty(_settings.GetType(), _settings, _yamlPipelineProperty, InstanceMembers, allowNull: true) is IEnumerable pipeline)
                {
                    foreach (var entry in pipeline)
                    {
                        foreach (var variant in NonNullPropertyValues(entry))
                        {
                            var exporter = GetProperty(variant.GetType(), variant, "Exporter", InstanceMembers, allowNull: true);
                            if (exporter == null)
                            {
                                continue;
                            }

                            foreach (var property in exporter.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                            {
                                if (property.GetIndexParameters().Length == 0 && property.GetValue(exporter) != null)
                                {
                                    names.Add(ToYamlName(property.Name));
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                var exporters = (IEnumerable)GetProperty(_settings.GetType(), _settings, _exportersProperty, InstanceMembers);
                names.AddRange(exporters.Cast<object>().Select(e => e.ToString().ToLowerInvariant()));
            }

            return names.Count == 0 ? "none" : string.Join(",", names.Distinct());
        }

        private static IEnumerable<object> NonNullPropertyValues(object instance)
        {
            if (instance == null)
            {
                yield break;
            }

            foreach (var property in instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var value = property.GetIndexParameters().Length == 0 ? property.GetValue(instance) : null;
                if (value != null)
                {
                    yield return value;
                }
            }
        }

        private static PropertyInfo GetPropertyInfo(Type type, string name, BindingFlags flags)
        {
            return type.GetProperty(name, flags) ?? throw new MissingMemberException(type.FullName, name);
        }

        private static object GetProperty(Type type, object instance, string name, BindingFlags flags, bool allowNull = false)
        {
            var value = GetPropertyInfo(type, name, flags).GetValue(instance);
            if (value == null && !allowNull)
            {
                throw new InvalidOperationException(type.FullName + "." + name + " is null.");
            }

            return value;
        }
    }
}
