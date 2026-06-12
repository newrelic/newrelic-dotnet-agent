# Navigation Map

Where to look in the live source for each question family. Anchors are
**coarse on purpose** — directory + class + grep string, never line numbers —
so this map cannot go stale. Always grep the search terms against the current
tree and read the code before answering; generate citations live.

Lean on the repo's `CLAUDE.md` sub-docs as the first-stop orientation layer
(`src/claude-source.md`, `tests/claude-tests.md`, `docs/config-development.md`)
and add only the question-family anchors they don't already cover.

## Collector / region selection
**Answers:** How does the agent decide whether to connect to the US vs EU (or other region) collector?
**Start in repo docs:** `src/claude-source.md` §Agent Core "Data transport" (lists `ConnectionManager`); root `CLAUDE.md` §Configuration (env > config > server > defaults precedence)
**Directory:** `src/Agent/NewRelic/Agent/Core/DataTransport/`
**Key class(es):** `ConnectionInfo`, `DefaultConfiguration`
**Search terms:** `GetCollectorHost`, `collector.nr-data.net`, `accountRegionRegex`, `CollectorHost`
**Trace notes:** `ConnectionInfo.GetCollectorHost` is the decision point. An explicit `CollectorHost` (newrelic.config `service.host` / `NEW_RELIC_HOST`) wins; else the license key is matched against `accountRegionRegex` (`^.+?x`), the prefix (minus the trailing `x`) is spliced into `collector.nr-data.net`, and a non-region key falls back to `collector.newrelic.com`. License key resolved by `DefaultConfiguration.AgentLicenseKey`/`TryGetLicenseKey`. A collector redirect host overrides all of it afterward. Cross-agent data covered by `CollectorHostNameTests` + `collector_hostname.json`.

## License-key obfuscation
**Answers:** Does the agent obfuscate/mask the license key in logs, and how does that differ from the `obscuringKey` config feature?
**Start in repo docs:** none directly (root `CLAUDE.md` §Configuration covers precedence; no dedicated masking section)
**Directory:** `src/Agent/NewRelic/Agent/Core/Utilities/` (masking) and `src/Agent/NewRelic/Agent/Core/Configuration/` (obscuringKey)
**Key class(es):** `Strings`, `DataTransportAuditLogger`, `ConfigurationBridge`, `DefaultConfiguration`
**Search terms:** `ObfuscateLicenseKey`, `ObfuscateLicenseKeyInAuditLog`, `obscuringKey`, `NEW_RELIC_CONFIG_OBSCURING_KEY`
**Trace notes:** Masking lives in `Strings.ObfuscateLicenseKey` (keeps first 10 chars, stars the rest; all-stars if ≤10) and `Strings.ObfuscateLicenseKeyInAuditLog` (masks the `license_key=` URL param). Call sites: `DataTransportAuditLogger`, config-value debug logging (`ConfigurationBridge`), infinite-tracing gRPC headers (`DataStreamingService`), MSI installer. Do NOT conflate with `DefaultConfiguration.ObscuringKey` (`service.obscuringKey` / `NEW_RELIC_CONFIG_OBSCURING_KEY`), which Base64-deobfuscates encrypted *config values* (e.g. proxy password) and is unrelated to log masking — see the XSD doc for `obscuringKey`. Unit-tested in `StringsTest`.

## Log levels
**Answers:** What logging levels does the agent support, and where are they defined/parsed?
**Start in repo docs:** `src/claude-source.md` §"Debugging signals" (mentions `NEWRELIC_LOG_LEVEL=debug`); root `CLAUDE.md` (debug logs to `<home>/logs/`)
**Directory:** `src/Agent/NewRelic/Agent/Core/Logging/`
**Key class(es):** `LogLevelExtensions`
**Search terms:** `MapToSerilogLogLevel`, `IsLogLevelDeprecated`, `AuditLevel`, `TranslateLogLevel`
**Trace notes:** The documented set is on the `log/@level` attribute in `Configuration.xsd` (default `info`): off, error, warn, info, debug, finest, all. `LogLevelExtensions.MapToSerilogLogLevel` parses the string case-insensitively onto Serilog levels and accepts many deprecated aliases (verbose/fine/finer/trace/notice/alert/critical/emergency/fatal/severe) that still work but warn. `OFF` maps above Fatal (disables logging); `AUDIT` is not a level (treated as INFO with a warning — audit logging is enabled via the `auditLog` attribute). `TranslateLogLevel` converts back to the FINEST/DEBUG/INFO/WARN/ERROR log-file labels. Unit-tested in `LogLevelExtensionsTests`.

## Docker container-id detection
**Answers:** How does the agent determine the Docker container id?
**Start in repo docs:** none directly (Utilization is not covered in the `claude-*.md` sub-docs)
**Directory:** `src/Agent/NewRelic/Agent/Core/Utilization/`
**Key class(es):** `VendorInfo`, `DockerVendorModel`
**Search terms:** `GetDockerVendorInfo`, `/proc/self/mountinfo`, `/proc/self/cgroup`, `ContainerIdV2Regex`
**Trace notes:** `VendorInfo.GetVendors` only checks Docker when `UtilizationDetectDocker` is on and ECS info isn't already present. `VendorInfo.GetDockerVendorInfo` runs Linux-only: it tries cgroup v2 first by parsing `/proc/self/mountinfo` (`ContainerIdV2Regex`, a `/docker/containers/<64-hex>/` path), then falls back to cgroup v1 by parsing `/proc/self/cgroup` (`ContainerIdV1Regex`, a `cpu`-controller line with a 64-hex id). A match yields a `DockerVendorModel`. AWS ECS is a separate path that reads the `DockerId` token from the ECS metadata endpoint. Failures log at Finest and return null. Unit-tested in `VendorInfoTests` (ParsesV2 / ParsesV1 fallback).

## Attribute → payload mapping (queueDuration)
**Answers:** Where does `queueDuration` get set, and which harvested payload(s) does it ship in?
**Start in repo docs:** `src/claude-source.md` §Agent Core "Aggregators" + "Data pipeline" (transformers → aggregators → events/traces/spans); §Layout lists the `Attributes/` directory
**Directory:** `src/Agent/NewRelic/Agent/Core/` (`Attributes`, `Transactions`, `Transformers/TransactionTransformer`, `WireModels`, `Metrics`) and the AspNet wrapper
**Key class(es):** `HttpContextActions`, `Transaction`/`TransactionMetadata`, `AttributeDefinitionService`, `TransactionAttributeMaker`, `TransactionTransformer`, `MetricWireModel`
**Search terms:** `SetQueueTime`, `QueueDuration`, `queueDuration`, `WebFrontend/QueueTime`
**Trace notes:** Value originates in `HttpContextActions.StoreQueueTime` (now − worker-request start) → `Transaction.SetQueueTime` → `TransactionMetadata.SetQueueTime`/`QueueTime`. The attribute is DEFINED in `AttributeDefinitionService.QueueDuration` — an Intrinsic named `queueDuration` (value = `TotalSeconds`) applying ONLY to `AttributeDestinations.TransactionEvent` and `ErrorEvent` (not traces, not spans). `TransactionAttributeMaker` sets it; `TransactionEventMaker`/`ErrorEventMaker` carry it into wire models. SEPARATELY the same queue time drives the unscoped metric `WebFrontend/QueueTime` via `TransactionTransformer` + `MetricWireModel`/`MetricNames` — a metric, not the attribute. `AttributeDefinitionService` `.AppliesTo(...)` is the single source of truth for which payload carries an attribute. Unit-tested in `TransactionAttributeMakerTests`.

## RabbitMQ attributes
**Answers:** What attributes does the agent collect for RabbitMQ?
**Start in repo docs:** `src/claude-source.md` §Agent Core "Segments" (message-broker category) and §"Creating a wrapper"
**Directory:** `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/RabbitMq/` and `src/Agent/NewRelic/Agent/Core/Segments/`
**Key class(es):** `RabbitMqHelper`, `BasicPublishWrapper`, `BasicPublishWrapperLegacy`, `HandleBasicDeliverWrapper`, `MessageBrokerSegmentData`, `AttributeDefinitionService`
**Search terms:** `StartMessageBrokerSegment`, `MessageBrokerSegmentData`, `routingKey`, `server.address`, `messaging.rabbitmq.destination.routing_key`
**Trace notes:** Publish wrappers call `RabbitMqHelper.CreateSegmentForPublishWrappers`/`...6Plus`; consume goes through `HandleBasicDeliverWrapper.BeforeWrappedMethod`. Both call `transaction.StartMessageBrokerSegment(...)` with destination (from routing key), the RabbitMQ vendor constant, `routingKey`, and `serverAddress`/`serverPort` resolved by reflection (`GetServerAddress`/`GetServerPort` → `Session.Connection.Endpoint.HostName`/`Port`). These land on `MessageBrokerSegmentData` fields; `SetSpanTypeSpecificAttributes` emits `span.kind` (producer/consumer) plus the named attributes defined in `AttributeDefinitionService`: `server.address`, `server.port`, `messaging.system`, `messaging.destination.name`, `message.queueName` (consumer), `message.routingKey`, `messaging.rabbitmq.destination.routing_key`, `messaging.destination_publish.name` (consumer). Distributed-trace context propagates through the message `Headers`. Server address/port are best-effort (omitted, logged at Warn, if reflection fails). Behavior asserted in `RabbitMqTests` (unbounded).

## Transaction naming (Web / WebAPI / MVC)
**Answers:** How are WebAPI transactions named, and how does that compare to MVC/Web generally?
**Start in repo docs:** `src/claude-source.md` §Agent Core "Transactions" (`TransactionName`, `TransactionMetricNameMaker`); `tests/claude-tests.md` example asserting `WebTransaction/MVC/Home/Index`
**Directory:** `src/Agent/NewRelic/Agent/Core/Transactions/` and `Core/Transformers/TransactionTransformer/`, entry point in `Extensions/Providers/Wrapper/WebApi2/`
**Key class(es):** `AsyncApiControllerActionInvoker` (WebApi2 wrapper, in `InvokeActionAsyncWrapper.cs`), `TransactionName`, `TransactionMetricNameMaker`, `MetricNames`
**Search terms:** `SetWebTransactionName`, `WebTransactionType`, `ForWebTransaction`, `WebTransactionPrefix`
**Trace notes:** The WebApi2 wrapper builds `{controller}/{action}` and calls `transaction.SetWebTransactionName(WebTransactionType.WebAPI, ...)`. `Transaction.SetWebTransactionName` → `TransactionName.ForWebTransaction` uses the enum name as the Category (`WebAPI`), giving `UnprefixedName = WebAPI/{controller}/{action}`. `TransactionMetricNameMaker.GetTransactionMetricName` prepends `MetricNames.WebTransactionPrefix` (`WebTransaction`) for web transactions → `WebTransaction/WebAPI/{controller}/{action}`. MVC is identical with `WebTransactionType.MVC` → `WebTransaction/MVC/{controller}/{action}`. Non-web transactions use `OtherTransactionPrefix` (`OtherTransaction`). Names can still be rewritten by URL/metric rules or higher-priority sources. Asserted in `AgentFeatures/Rules.cs`, `CustomAttributesArraySupport.cs`, `CatEnabledChainedTaskResultRestSharp.cs` (integration).

## Library version support (instrumentation.xml version ranges)
**Answers:** Does the agent support a given library version (concrete case: MongoDB.Driver 3.7)? `maxVersion` is EXCLUSIVE (strictly less than).
**Start in repo docs:** root `CLAUDE.md` §"instrumentation.xml version ranges" (maxVersion exclusive); `src/claude-source.md` §"Creating a wrapper" (maxVersion exclusive)
**Directory:** `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/MongoDb26/` (modern driver) and `.../MongoDb/` (legacy 1.x)
**Key class(es):** `MongoDb26` `Instrumentation.xml` `<tracerFactory>`/`<match>` entries, `MongoDb` `Instrumentation.xml`
**Search terms:** `MongoDB.Driver`, `MongoCollectionImpl`, `minVersion="3.0.0"`, `maxVersion="3.0.0"`
**Trace notes:** Open the `Instrumentation.xml` and read the `<match>` entries' `minVersion`/`maxVersion` (remember `maxVersion` is EXCLUSIVE). For MongoDB.Driver: most `MongoDb26` matches carry NO version bounds, so they apply to every version where the type exists (including 3.7). The only gated entries switch type/assembly at the driver's 3.0 repackaging boundary — pre-3.0 used `MongoDB.Driver.Core` (`maxVersion="3.0.0"`) and `MongoDatabaseImpl` (`maxVersion="3.0.0"`); 3.0+ uses `MongoDB.Driver` (`minVersion="3.0.0"`) and the renamed `MongoDatabase`, so at 3.7 the `minVersion="3.0.0"` branches apply. The legacy `MongoDb` wrapper covers the old 1.x `MongoCollection` API. `CHANGELOG.md` records "Add support for MongoDB.Driver 3.x and above." Tested driver range pinned in the integration `MFALatestPackages.csproj` / `MultiFunctionApplicationHelpers.csproj` (oldest ~2.3 through latest 3.9); no upper cap on the core matches.
