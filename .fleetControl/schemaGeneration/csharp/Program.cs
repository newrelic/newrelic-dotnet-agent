// Fleet Control Config Schema Generator — .NET Agent
//
// Reads Configuration.xsd from this repo's working tree and writes JSON
// Schema Draft 2020-12 to ../../schemas/config.json. Sole source of truth
// for .fleetControl/schemas/config.json.
//
// Run from this directory:
//     dotnet run                                                  # regenerate config.json (no version bump)
//     dotnet run -- --ci                                          # also bump version in configurationDefinitions.yml
//     CONFIGURATION_XSD=/path/to/Configuration.xsd dotnet run     # override XSD source
//
// Exit codes:
//     0 — no schema changes (or first run)
//     1 — schema changed (CI should commit the updated files)

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Json.Schema;
using YamlDotNet.RepresentationModel;

string scriptDir = Path.GetDirectoryName(SourcePath())!;
// schemas/ lives at .fleetControl/schemas/ — two levels above this csharp/ folder.
string schemaDir = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "schemas"));
string schemaPath = Path.Combine(schemaDir, "config.json");
string configDefPath = Path.GetFullPath(Path.Combine(scriptDir, "..", "..", "configurationDefinitions.yml"));
// In-tree XSD: .fleetControl/schemaGeneration/csharp/ → repo root is three levels up.
string defaultXsdPath = Path.GetFullPath(Path.Combine(
    scriptDir, "..", "..", "..",
    "src", "Agent", "NewRelic", "Agent", "Core", "Config", "Configuration.xsd"));

// Parse CLI flags: --ci (apply bump) and --bump=major|minor|patch|none (override)
bool ciMode = args.Contains("--ci");
string? overrideBump = args
    .FirstOrDefault(a => a.StartsWith("--bump=", StringComparison.Ordinal))
    ?.Substring("--bump=".Length);
if (overrideBump is not null
    && overrideBump != "major" && overrideBump != "minor"
    && overrideBump != "patch" && overrideBump != "none")
{
    Console.Error.WriteLine($"Invalid --bump value: {overrideBump}");
    return 2;
}

string xsdText = LoadXsd(defaultXsdPath);
var (schema, omissions) = SchemaGenerator.Generate(xsdText);

ValidateMetaSchema(schema);

JsonObject? oldSchema = LoadExisting(schemaPath);

WriteSchema(schema, schemaPath);
Console.WriteLine($"Wrote:   {schemaPath}");
PrintOmissions(omissions);

if (oldSchema is null)
{
    Console.WriteLine();
    Console.WriteLine("First run — schema created.");
    return 0;
}

List<Change> changes = SchemaClassifier.Classify(oldSchema, schema);

if (changes.Count > 0)
{
    var breaking = changes.Where(c => c.Severity == "breaking").ToList();
    var additive = changes.Where(c => c.Severity == "additive").ToList();
    var cosmetic = changes.Where(c => c.Severity == "cosmetic").ToList();
    Console.WriteLine();
    Console.WriteLine($"Schema changes ({changes.Count}):");
    if (breaking.Count > 0)
    {
        Console.WriteLine($"  BREAKING ({breaking.Count}):");
        foreach (var c in breaking) Console.WriteLine($"    {c.Render()}");
    }
    if (additive.Count > 0)
    {
        Console.WriteLine($"  ADDITIVE ({additive.Count}):");
        foreach (var c in additive) Console.WriteLine($"    {c.Render()}");
    }
    if (cosmetic.Count > 0)
    {
        Console.WriteLine($"  COSMETIC ({cosmetic.Count}):");
        foreach (var c in cosmetic) Console.WriteLine($"    {c.Render()}");
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("No schema changes.");
}

string autoBump = RecommendBump(changes);
string chosen = overrideBump ?? autoBump;
var (oldV, newV) = BumpVersion(configDefPath, chosen, write: ciMode);
Console.WriteLine();
if (chosen == "none" || newV == oldV)
{
    Console.WriteLine($"Recommended bump: none ({oldV} unchanged)");
}
else if (overrideBump is not null && overrideBump != autoBump)
{
    Console.WriteLine($"Recommended bump: {autoBump} → overridden to {chosen} ({oldV} → {newV})");
}
else
{
    Console.WriteLine($"Recommended bump: {chosen} ({oldV} → {newV})");
}
if (ciMode && newV != oldV)
{
    Console.WriteLine($"Wrote:   {configDefPath}");
}

return changes.Count == 0 ? 0 : 1;

// --- helpers -------------------------------------------------------------

static string SourcePath([CallerFilePath] string path = "") => path;

static string LoadXsd(string defaultPath)
{
    var path = Environment.GetEnvironmentVariable("CONFIGURATION_XSD");
    if (string.IsNullOrEmpty(path)) path = defaultPath;
    Console.WriteLine($"Reading XSD: {path}");
    return File.ReadAllText(path);
}

static JsonObject? LoadExisting(string path)
{
    if (!File.Exists(path)) return null;
    try { return JsonNode.Parse(File.ReadAllText(path)) as JsonObject; }
    catch (JsonException) { return null; }
}

static void WriteSchema(JsonObject schema, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        // Match Python json.dump output: standard JSON escaping (\" not "),
        // leave ' / & unescaped. UnsafeRelaxedJsonEscaping is misnamed — it just
        // means "don't HTML-encode, just JSON-encode."
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    File.WriteAllText(path, schema.ToJsonString(options) + "\n");
}

static void ValidateMetaSchema(JsonObject schema)
{
    // Validate the generated schema against JSON Schema 2020-12.
    // JsonSchema.Net's MetaSchemas.Draft202012 ships built-in (no network fetch);
    // we evaluate our schema-as-JSON against it. Hard-fails on any error —
    // a structurally invalid schema should never be written.
    var schemaNode = JsonNode.Parse(schema.ToJsonString());
    var results = MetaSchemas.Draft202012.Evaluate(schemaNode, new EvaluationOptions
    {
        OutputFormat = OutputFormat.List,
    });
    if (results.IsValid)
    {
        Console.WriteLine("Meta-schema validation passed (Draft 2020-12)");
        return;
    }
    Console.Error.WriteLine("Meta-schema validation FAILED:");
    foreach (var detail in results.Details)
    {
        if (detail.IsValid || detail.Errors is null) continue;
        foreach (var (keyword, message) in detail.Errors)
        {
            Console.Error.WriteLine($"  {detail.InstanceLocation} [{keyword}]: {message}");
        }
    }
    Environment.Exit(2);
}

// ---- semver bump helpers ------------------------------------------------

static string RecommendBump(List<Change> changes)
{
    if (changes.Any(c => c.Severity == "breaking")) return "major";
    if (changes.Any(c => c.Severity == "additive")) return "minor";
    if (changes.Any(c => c.Severity == "cosmetic")) return "patch";
    return "none";
}

static string ApplyBump(string version, string bump)
{
    if (bump == "none") return version;
    var parts = version.Split('.');
    if (parts.Length != 3 || !parts.All(p => int.TryParse(p, out _)))
    {
        throw new ArgumentException($"version '{version}' is not semver MAJOR.MINOR.PATCH");
    }
    int major = int.Parse(parts[0]);
    int minor = int.Parse(parts[1]);
    int patch = int.Parse(parts[2]);
    return bump switch
    {
        "major" => $"{major + 1}.0.0",
        "minor" => $"{major}.{minor + 1}.0",
        "patch" => $"{major}.{minor}.{patch + 1}",
        _ => throw new ArgumentException($"unknown bump kind '{bump}'"),
    };
}

static (string OldVersion, string NewVersion) BumpVersion(string yamlPath, string bump, bool write)
{
    // Parse with YamlDotNet for structural validation (find the version),
    // then write with a targeted regex replacement so the rest of the file
    // (comments, ordering, quoting) is preserved byte-for-byte. Same approach
    // every other generator uses → trivial cross-language parity.
    string text = File.ReadAllText(yamlPath);
    var stream = new YamlStream();
    stream.Load(new StringReader(text));
    if (stream.Documents.Count == 0
        || stream.Documents[0].RootNode is not YamlMappingNode root
        || !root.Children.TryGetValue(new YamlScalarNode("configurationDefinitions"), out var defsNode)
        || defsNode is not YamlSequenceNode defs
        || defs.Children.Count == 0
        || defs.Children[0] is not YamlMappingNode first
        || !first.Children.TryGetValue(new YamlScalarNode("version"), out var versionNode)
        || versionNode is not YamlScalarNode versionScalar
        || versionScalar.Value is null)
    {
        throw new InvalidDataException(
            $"{yamlPath}: configurationDefinitions[0].version not found"
        );
    }
    string oldVersion = versionScalar.Value;
    string newVersion = ApplyBump(oldVersion, bump);
    if (write && newVersion != oldVersion)
    {
        var versionLine = new Regex(@"^(\s*version:\s*)(\S+)(\s*)$", RegexOptions.Multiline);
        int matches = 0;
        string newText = versionLine.Replace(text, m =>
        {
            matches++;
            return $"{m.Groups[1].Value}{newVersion}{m.Groups[3].Value}";
        });
        if (matches != 1)
        {
            throw new InvalidDataException(
                $"{yamlPath}: expected exactly 1 'version:' line, found {matches}"
            );
        }
        File.WriteAllText(yamlPath, newText);
    }
    return (oldVersion, newVersion);
}

static void PrintOmissions(IReadOnlyList<(string Path, string Reason)> omissions)
{
    if (omissions.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("No settings omitted.");
        return;
    }
    Console.WriteLine();
    Console.WriteLine($"Omitted {omissions.Count} XSD nodes (private / internal / out of scope):");
    foreach (var (p, reason) in omissions.OrderBy(o => o.Path, StringComparer.Ordinal))
    {
        Console.WriteLine($"  - {p}");
        Console.WriteLine($"      reason: {reason}");
    }
}

// =========================================================================
// Schema generator — public-facing filter + XSD walker.
// =========================================================================
static class SchemaGenerator
{
    static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";

    // Skip any XSD node whose xs:documentation contains one of these
    // (case-insensitive). Each entry is paired with the reason it triggers.
    static readonly (string Needle, string Reason)[] DocKeywords =
    [
        ("deprecated",              "marked deprecated in XSD documentation"),
        ("experimental",            "marked experimental in XSD documentation"),
        ("do not use",              "marked 'do not use' in XSD documentation"),
        ("do not modify",           "marked 'do not modify' in XSD documentation"),
        ("internal use",            "marked internal-use in XSD documentation"),
        ("agent debugging",         "agent-debugging-only setting"),
        ("debugger to be attached", "agent-debugging-only setting"),
    ];

    // Curated path-based omissions (heuristic can't catch by docstring alone).
    // Paths use XSD element names verbatim; "@name" denotes an attribute.
    static readonly Dictionary<string, string> ExcludePaths = new()
    {
        ["configuration/instrumentation"]               = "native CLR instrumentation rules — defined in code/XML on the host, not Fleet Control scope",
        ["configuration/customInstrumentationEditor"]   = "developer-tooling configuration for the local CIE UI, not a runtime knob",
        ["configuration/applicationPools"]              = "IIS application-pool include/exclude — host-level installer concern, not config push",
        ["configuration/processHost"]                   = "process-host knobs configured by the installer/profiler, not by users",
        ["configuration/appSettings"]                   = "free-form key/value bag with no schema — incompatible with a typed JSON Schema",
        ["configuration/parameterGroups"]               = "legacy attribute capture model superseded by the `attributes` element",
        ["configuration/requestParameters"]             = "legacy attribute capture model superseded by the `attributes` element",
        ["configuration/customParameters"]              = "legacy attribute capture model superseded by the `attributes` element",
        ["configuration/diagnostics"]                   = "agent-internal performance instrumentation, not user-tunable behavior",
        ["configuration/securityPoliciesToken"]         = "LASP (Language Agent Security Policies) token — provisioning concern, not standard config",

        ["configuration/@maxStackTraceLines"]           = "agent internals — controls stack-frame capture depth, not user-facing",
        ["configuration/@threadProfilingEnabled"]       = "thread profiling is an on-demand UI feature, toggled per-session not by static config",
        ["configuration/@crossApplicationTracingEnabled"] = "legacy CAT — superseded by distributed tracing",
        ["configuration/@timingPrecision"]              = "agent-internal timer resolution; advanced/rarely-tuned",
        ["configuration/@serverlessModeEnabled"]        = "lambda/serverless deployment context — irrelevant to k8s Fleet Control delivery",

        ["configuration/service/obscuringKey"]                    = "decrypts on-disk obfuscated config values; meaningless when config arrives via env vars",
        ["configuration/service/@disableFileSystemWatcher"]       = "controls hot-reload of on-disk files; irrelevant when config arrives via env vars",
        ["configuration/service/@host"]                           = "collector host override — set by region/datacenter, not user-facing app config",
        ["configuration/service/@port"]                           = "collector port override — set by region/datacenter, not user-facing app config",
        ["configuration/service/@requestTimeout"]                 = "transport-layer tuning — rarely changed, not Fleet Control's audience",
        ["configuration/service/@completeTransactionsOnThread"]   = "advanced threading override; risk of misconfiguration outweighs the use case",
        ["configuration/service/@forceNewTransactionOnNewThread"] = "advanced threading override; risk of misconfiguration outweighs the use case",
        ["configuration/service/@sendEnvironmentInfo"]            = "agent diagnostic metadata reporting; not a behavioral toggle",
    };

    // Curated enums for attributes the XSD intentionally types as xs:string
    // (e.g. log/@level — see the comment in Configuration.xsd above the
    // attribute: an XSD enumeration would induce case sensitivity at parse
    // time and break legacy newrelic.config files using "DEBUG" or "Debug").
    // For Fleet Control's UI we want a real enum (dropdown). We keep the
    // existing xs:documentation as the property's description so the
    // case-insensitivity note travels with the schema.
    static readonly Dictionary<string, string[]> EnumOverrides = new()
    {
        ["configuration/log/@level"]               = ["off", "error", "warn", "info", "debug", "finest", "all"],
        ["configuration/browserMonitoring/@loader"] = ["rum", "full", "none"],
    };

    // XSD primitive type → JSON Schema type
    static readonly Dictionary<string, string> PrimitiveTypeMap = new()
    {
        ["xs:string"]  = "string",
        ["xs:boolean"] = "boolean",
        ["xs:int"]     = "integer",
        ["xs:integer"] = "integer",
        ["xs:long"]    = "integer",
        ["xs:short"]   = "integer",
        ["xs:decimal"] = "number",
        ["xs:float"]   = "number",
        ["xs:double"]  = "number",
    };

    public static (JsonObject Schema, IReadOnlyList<(string Path, string Reason)> Omissions) Generate(string xsdText)
    {
        var doc = XDocument.Parse(xsdText);
        var simpleTypes = BuildSimpleTypeRegistry(doc.Root!);

        var configEl = doc.Root!.Elements(Xs + "element")
            .FirstOrDefault(e => (string?)e.Attribute("name") == "configuration")
            ?? throw new InvalidDataException("Could not find <xs:element name='configuration'> in XSD");

        var configCt = configEl.Element(Xs + "complexType")!;
        var builder = new SchemaBuilder(simpleTypes);

        // Build into the top-level schema object directly so we don't have to
        // reparent JsonNode children later.
        var schema = new JsonObject
        {
            ["$schema"]     = "https://json-schema.org/draft/2020-12/schema",
            ["title"]       = "New Relic .NET Agent Configuration",
            ["description"] =
                "Fleet Control configuration schema for the New Relic .NET agent. " +
                "Generated from src/Agent/NewRelic/Agent/Core/Config/Configuration.xsd. " +
                "x-xml-attributes / x-xml-elements record the order required when " +
                "rendering this config back into a newrelic.config XML file.",
        };
        builder.PopulateComplexType(configCt, "configuration", schema);
        // Root has no x-xml-kind because it isn't itself an XML element/attribute child.
        schema.Remove("x-xml-kind");

        return (schema, builder.Omissions);
    }

    static Dictionary<string, JsonObject> BuildSimpleTypeRegistry(XElement root)
    {
        var registry = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var st in root.Elements(Xs + "simpleType"))
        {
            var name = (string?)st.Attribute("name");
            if (string.IsNullOrEmpty(name)) continue;
            var entry = ParseSimpleType(st);
            if (entry is not null) registry[name] = entry;
        }
        return registry;
    }

    static JsonObject? ParseSimpleType(XElement st)
    {
        var restriction = st.Element(Xs + "restriction");
        if (restriction is null) return null;
        var baseType = (string?)restriction.Attribute("base") ?? "xs:string";
        var jsonType = PrimitiveTypeMap.TryGetValue(baseType, out var jt) ? jt : "string";

        var entry = new JsonObject { ["type"] = jsonType };
        var enums = restriction.Elements(Xs + "enumeration")
                               .Select(e => (string?)e.Attribute("value"))
                               .Where(v => v is not null)
                               .ToArray();
        if (enums.Length > 0)
        {
            var arr = new JsonArray();
            foreach (var v in enums) arr.Add(v);
            entry["enum"] = arr;
        }
        return entry;
    }

    sealed class SchemaBuilder(Dictionary<string, JsonObject> simpleTypes)
    {
        readonly List<(string Path, string Reason)> _omissions = new();

        public IReadOnlyList<(string Path, string Reason)> Omissions => _omissions;

        public JsonObject BuildComplexType(XElement ctNode, string parentPath)
        {
            var target = new JsonObject();
            PopulateComplexType(ctNode, parentPath, target);
            return target;
        }

        public void PopulateComplexType(XElement ctNode, string parentPath, JsonObject target)
        {
            var properties     = new JsonObject();
            var attributeOrder = new JsonArray();
            var elementOrder   = new JsonArray();
            var required       = new JsonArray();

            // 1. Attributes (direct children of complexType)
            foreach (var attr in ctNode.Elements(Xs + "attribute"))
            {
                var (name, prop) = BuildAttribute(attr, parentPath);
                if (name is null || prop is null) continue;

                bool isRequired = false;
                if (prop.ContainsKey("x-required"))
                {
                    isRequired = prop["x-required"]?.GetValue<bool>() ?? false;
                    prop.Remove("x-required");
                }
                properties[name] = prop;
                attributeOrder.Add(name);
                if (isRequired) required.Add(name);
            }

            // 2. Child elements: walk xs:all / xs:sequence / xs:choice in document order
            foreach (var container in ctNode.Elements())
            {
                if (container.Name.Namespace == Xs &&
                    container.Name.LocalName is "all" or "sequence" or "choice")
                {
                    WalkChildren(container, parentPath, properties, elementOrder, required);
                }
            }

            target["type"] = "object";
            target["properties"] = properties;
            target["additionalProperties"] = true;
            if (attributeOrder.Count > 0) target["x-xml-attributes"] = attributeOrder;
            if (elementOrder.Count > 0)   target["x-xml-elements"]   = elementOrder;
            if (required.Count > 0)       target["required"]         = required;
        }

        void WalkChildren(XElement container, string parentPath,
                          JsonObject properties, JsonArray elementOrder, JsonArray required)
        {
            foreach (var child in container.Elements())
            {
                if (child.Name.Namespace != Xs) continue;
                var localName = child.Name.LocalName;
                if (localName == "element")
                {
                    var (name, prop) = BuildElement(child, parentPath);
                    if (name is null || prop is null) continue;
                    properties[name] = prop;
                    elementOrder.Add(name);

                    var minOccurs = (string?)child.Attribute("minOccurs");
                    if (minOccurs == "1" && !parentPath.EndsWith("/configuration"))
                    {
                        required.Add(name);
                    }
                }
                else if (localName is "sequence" or "choice" or "all")
                {
                    WalkChildren(child, parentPath, properties, elementOrder, required);
                }
            }
        }

        (string? Name, JsonObject? Prop) BuildAttribute(XElement attrNode, string parentPath)
        {
            var name = (string?)attrNode.Attribute("name");
            if (string.IsNullOrEmpty(name)) return (null, null);
            var path = $"{parentPath}/@{name}";

            if (ExcludePaths.TryGetValue(path, out var reason))
            {
                _omissions.Add((path, reason));
                return (null, null);
            }

            var doc = GetDoc(attrNode);
            if (TryFilterByDoc(doc, out var docReason))
            {
                _omissions.Add((path, docReason!));
                return (null, null);
            }

            // Inline simpleType wins over named-type reference
            var prop = InlineSimpleType(attrNode) ??
                       ResolveSimpleType((string?)attrNode.Attribute("type") ?? "xs:string");
            ApplyEnumOverride(prop, path);

            string jsonType = prop["type"]?.GetValue<string>() ?? "string";
            var defaultRaw = (string?)attrNode.Attribute("default");
            var defaultNode = CoerceDefault(defaultRaw, jsonType);
            if (defaultNode is not null) prop["default"] = defaultNode;
            if (!string.IsNullOrEmpty(doc)) prop["description"] = doc;
            if ((string?)attrNode.Attribute("use") == "required")
            {
                prop["x-required"] = true;
                // JSON Schema's `required` only mandates presence — for required
                // strings, also disallow the empty string so an unfilled value
                // doesn't silently satisfy the constraint.
                if ((string?)prop["type"] == "string" && !prop.ContainsKey("minLength"))
                {
                    prop["minLength"] = 1;
                }
            }
            prop["x-xml-kind"] = "attribute";
            return (name, prop);
        }

        (string? Name, JsonObject? Prop) BuildElement(XElement elNode, string parentPath)
        {
            var name = (string?)elNode.Attribute("name");
            if (string.IsNullOrEmpty(name)) return (null, null);
            var path = $"{parentPath}/{name}";

            if (ExcludePaths.TryGetValue(path, out var reason))
            {
                _omissions.Add((path, reason));
                return (null, null);
            }

            var doc = GetDoc(elNode);
            if (string.IsNullOrEmpty(doc))
            {
                // Doc sometimes lives on the inline complexType, not the element.
                var inlineCt = elNode.Element(Xs + "complexType");
                if (inlineCt is not null) doc = GetDoc(inlineCt);
            }
            if (TryFilterByDoc(doc, out var docReason))
            {
                _omissions.Add((path, docReason!));
                return (null, null);
            }

            var maxOccurs = (string?)elNode.Attribute("maxOccurs") ?? "1";
            bool isArray  = maxOccurs is not "0" and not "1";

            var typeAttr = (string?)elNode.Attribute("type");
            if (!string.IsNullOrEmpty(typeAttr))
            {
                // Simple-typed element, e.g. <element name="labels" type="xs:string"/>
                var inner = ResolveSimpleType(typeAttr);
                if (!string.IsNullOrEmpty(doc)) inner["description"] = doc;
                inner["x-xml-kind"] = "element";

                if (isArray)
                {
                    var itemsObj = new JsonObject();
                    foreach (var kv in inner.ToList())
                    {
                        if (kv.Key is "x-xml-kind" or "description") continue;
                        var node = kv.Value;
                        inner.Remove(kv.Key);
                        itemsObj[kv.Key] = node;
                    }
                    var wrapper = new JsonObject { ["type"] = "array", ["items"] = itemsObj, ["x-xml-kind"] = "element" };
                    if (!string.IsNullOrEmpty(doc)) wrapper["description"] = doc;
                    return (name, wrapper);
                }
                return (name, inner);
            }

            var complexType = elNode.Element(Xs + "complexType");
            if (complexType is null)
            {
                var leaf = new JsonObject { ["type"] = "string", ["x-xml-kind"] = "element" };
                if (!string.IsNullOrEmpty(doc)) leaf["description"] = doc;
                return (name, leaf);
            }

            var obj = BuildComplexType(complexType, path);
            if (!string.IsNullOrEmpty(doc)) obj["description"] = doc;
            obj["x-xml-kind"] = "element";

            if (isArray)
            {
                var items = new JsonObject();
                foreach (var kv in obj.ToList())
                {
                    if (kv.Key == "x-xml-kind") continue;
                    var node = kv.Value;
                    obj.Remove(kv.Key);
                    items[kv.Key] = node;
                }
                var wrapper = new JsonObject { ["type"] = "array", ["items"] = items, ["x-xml-kind"] = "element" };
                if (!string.IsNullOrEmpty(doc)) wrapper["description"] = doc;
                return (name, wrapper);
            }
            return (name, obj);
        }

        JsonObject InlineSimpleType(XElement node)
        {
            var st = node.Element(Xs + "simpleType");
            if (st is null) return null!;
            var parsed = ParseSimpleType(st);
            return parsed!;
        }

        JsonObject ResolveSimpleType(string typeAttr)
        {
            if (PrimitiveTypeMap.TryGetValue(typeAttr, out var jt))
                return new JsonObject { ["type"] = jt };
            var bare = typeAttr.Contains(':') ? typeAttr.Split(':', 2)[1] : typeAttr;
            if (simpleTypes.TryGetValue(bare, out var entry))
            {
                // Clone so we don't reparent a shared node.
                return CloneJsonObject(entry);
            }
            return new JsonObject { ["type"] = "string" };
        }

        static JsonObject CloneJsonObject(JsonObject src)
        {
            // ToJsonString round-trip is the simplest way to deep-clone a JsonNode.
            return (JsonNode.Parse(src.ToJsonString()) as JsonObject)!;
        }

        static void ApplyEnumOverride(JsonObject prop, string path)
        {
            if (!EnumOverrides.TryGetValue(path, out var values)) return;
            var arr = new JsonArray();
            foreach (var v in values) arr.Add(v);
            prop["enum"] = arr;
        }

        static string GetDoc(XElement node)
        {
            var docEl = node.Element(Xs + "annotation")?.Element(Xs + "documentation");
            if (docEl is null || string.IsNullOrEmpty(docEl.Value)) return string.Empty;
            return Regex.Replace(docEl.Value, @"\s+", " ").Trim();
        }

        static bool TryFilterByDoc(string doc, out string? reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(doc)) return false;
            var lower = doc.ToLowerInvariant();
            foreach (var (needle, r) in DocKeywords)
            {
                if (lower.Contains(needle, StringComparison.Ordinal))
                {
                    reason = r;
                    return true;
                }
            }
            return false;
        }

        static JsonNode? CoerceDefault(string? raw, string jsonType)
        {
            if (raw is null) return null;
            return jsonType switch
            {
                "boolean" => JsonValue.Create(string.Equals(raw.Trim(), "true", StringComparison.OrdinalIgnoreCase)),
                "integer" => long.TryParse(raw, out var i) ? JsonValue.Create(i) : null,
                // Match Python float(): whole numbers render as "60000.0", not "60000".
                // System.Text.Json's shortest-round-trip representation drops the .0,
                // so we parse a literal to preserve the format.
                "number"  => double.TryParse(raw, System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out var d)
                                ? JsonNode.Parse(d % 1 == 0
                                    ? d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                    : d.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                                : null,
                _ => JsonValue.Create(raw),
            };
        }
    }
}

// =========================================================================
// Schema classifier — severity-aware comparison of `properties` maps.
// Severities map to semver bumps: breaking → major, additive → minor,
// cosmetic → patch.
// =========================================================================

record Change(string Path, string Kind, string Severity, string Detail)
{
    public string Render()
    {
        string sym = Kind switch
        {
            "added" => "+",
            "removed" => "-",
            _ => "~",
        };
        return string.IsNullOrEmpty(Detail) ? $"{sym} {Path}" : $"{sym} {Path}: {Detail}";
    }
}

static class SchemaClassifier
{
    public static List<Change> Classify(JsonObject? oldSchema, JsonObject newSchema)
    {
        var changes = new List<Change>();
        Walk(oldSchema, newSchema, "", changes);
        return changes;
    }

    static void Walk(JsonObject? oldNode, JsonObject? newNode, string path, List<Change> changes)
    {
        // required: keys gained → breaking, keys lost → additive
        var oldReq = ToStringSet(oldNode?["required"] as JsonArray);
        var newReq = ToStringSet(newNode?["required"] as JsonArray);
        foreach (var k in newReq.Except(oldReq).OrderBy(s => s, StringComparer.Ordinal))
        {
            changes.Add(new Change(
                Path: string.IsNullOrEmpty(path) ? k : $"{path}.{k}",
                Kind: "required_added", Severity: "breaking",
                Detail: "now required"));
        }
        foreach (var k in oldReq.Except(newReq).OrderBy(s => s, StringComparer.Ordinal))
        {
            changes.Add(new Change(
                Path: string.IsNullOrEmpty(path) ? k : $"{path}.{k}",
                Kind: "required_removed", Severity: "additive",
                Detail: "no longer required"));
        }

        // additionalProperties: implicit true is the JSON Schema default
        bool oldAp = GetAdditionalProperties(oldNode);
        bool newAp = GetAdditionalProperties(newNode);
        if (oldAp && !newAp)
        {
            changes.Add(new Change(
                Path: string.IsNullOrEmpty(path) ? "<root>" : path,
                Kind: "additional_properties_tightened", Severity: "breaking",
                Detail: "additionalProperties: true → false"));
        }
        else if (!oldAp && newAp)
        {
            changes.Add(new Change(
                Path: string.IsNullOrEmpty(path) ? "<root>" : path,
                Kind: "additional_properties_loosened", Severity: "additive",
                Detail: "additionalProperties: false → true"));
        }

        // Walk properties
        var oldProps = (oldNode?["properties"] as JsonObject) ?? new JsonObject();
        var newProps = (newNode?["properties"] as JsonObject) ?? new JsonObject();
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var k in oldProps) keys.Add(k.Key);
        foreach (var k in newProps) keys.Add(k.Key);

        foreach (var key in keys)
        {
            var child = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
            bool inOld = oldProps.ContainsKey(key);
            bool inNew = newProps.ContainsKey(key);
            if (!inOld)
            {
                changes.Add(new Change(child, "added", "additive", "new property"));
                continue;
            }
            if (!inNew)
            {
                changes.Add(new Change(child, "removed", "breaking", "property removed"));
                continue;
            }

            var oldChild = oldProps[key] as JsonObject;
            var newChild = newProps[key] as JsonObject;
            if (oldChild is null || newChild is null) continue;

            bool bothObject = (string?)oldChild["type"] == "object"
                              && (string?)newChild["type"] == "object";
            if (bothObject)
            {
                Walk(oldChild, newChild, child, changes);
            }
            else
            {
                ClassifyLeaf(oldChild, newChild, child, changes);
            }
        }
    }

    static void ClassifyLeaf(JsonObject op, JsonObject np, string path, List<Change> changes)
    {
        var oldType = (string?)op["type"];
        var newType = (string?)np["type"];
        if (oldType != newType)
        {
            changes.Add(new Change(path, "type_changed", "breaking",
                $"type {oldType} → {newType}"));
        }

        var oldEnum = op["enum"] as JsonArray;
        var newEnum = np["enum"] as JsonArray;
        if (oldEnum is null && newEnum is not null)
        {
            changes.Add(new Change(path, "enum_introduced", "breaking",
                $"newly constrained to enum {newEnum.ToJsonString()}"));
        }
        else if (oldEnum is not null && newEnum is null)
        {
            changes.Add(new Change(path, "enum_removed_entirely", "additive",
                "enum constraint removed"));
        }
        else if (oldEnum is not null && newEnum is not null)
        {
            var oldSet = ToStringSet(oldEnum);
            var newSet = ToStringSet(newEnum);
            foreach (var v in oldSet.Except(newSet).OrderBy(s => s, StringComparer.Ordinal))
            {
                changes.Add(new Change(path, "enum_value_removed", "breaking",
                    $"enum value '{v}' removed"));
            }
            foreach (var v in newSet.Except(oldSet).OrderBy(s => s, StringComparer.Ordinal))
            {
                changes.Add(new Change(path, "enum_value_added", "additive",
                    $"enum value '{v}' added"));
            }
        }

        var oldDefault = op["default"]?.ToJsonString();
        var newDefault = np["default"]?.ToJsonString();
        if (oldDefault != newDefault)
        {
            changes.Add(new Change(path, "default_changed", "additive",
                $"default {oldDefault ?? "null"} → {newDefault ?? "null"}"));
        }

        var oldDesc = (string?)op["description"];
        var newDesc = (string?)np["description"];
        if (oldDesc != newDesc)
        {
            changes.Add(new Change(path, "description_changed", "cosmetic",
                "description updated"));
        }
    }

    static HashSet<string> ToStringSet(JsonArray? arr)
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        if (arr is null) return s;
        foreach (var n in arr)
        {
            var v = (string?)n;
            if (v is not null) s.Add(v);
        }
        return s;
    }

    static bool GetAdditionalProperties(JsonObject? node)
    {
        // JSON Schema default: additionalProperties is implicitly true.
        if (node is null || !node.ContainsKey("additionalProperties")) return true;
        var ap = node["additionalProperties"];
        return ap is JsonValue v && v.TryGetValue<bool>(out var b) ? b : true;
    }
}