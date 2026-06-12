# Completeness-Sweep Catalog

The full expansion of `SKILL.md` step 4. Each family is a recurring STRUCTURAL
trap — a reason the first matching code path is not the whole answer. Use it as
a checklist: for any family you cannot affirmatively rule out, go read the code.

These families are the product; the examples are **illustrations, not an answer
key** — they are coarse on purpose (behavior + class / grep term, no line
numbers) and must be re-confirmed against the live tree before you cite them
(SKILL.md step 5).

## INGEST / MATCHING

### Serial-gating pipeline
Instrumentation passes through several INDEPENDENT gates in series; any one can
silently NoOp a fully IL-injected method.
**Cue:** "is X instrumented / why didn't my method get traced".
**Example:** profiler XML match + version range + ignore list → managed
`WrapperMap`/`WrapperService` selection → runtime guards (`IsTransactionRequired`,
`CurrentSegment.IsLeaf`). Passing the match is not "done".

### Selection-among-N-providers-by-priority
One of N implementations wins by a numeric field, and the winner can change per
call site.
**Cue:** "which X is used", an interface with multiple implementations.
**Example:** `TransactionService` picks an `IContextStorage<IInternalTransaction>`
(created by the registered `IContextStorageFactory` instances) by highest
`Priority`, gated by a runtime `CanProvide` check. (Note: `Priority`/`CanProvide`
are on the storage object, not the factory.)

### Enablement-gate / first-match-wins / one-path-suppresses-another
Detection or enablement runs in an order where one path short-circuits another.
**Cue:** "does it detect X", cloud/vendor/utilization questions.
**Example:** ECS metadata presence suppresses Docker cgroup detection.

### One-input-silences-another
The presence of input A changes whether input B is even read.
**Cue:** header/precedence questions with two possible inputs.
**Example:** an inbound `traceparent` makes the legacy `newrelic` header be
ignored (regardless of the traceparent's validity).

## TRANSFORM

### Observer-effect / input mutation
The feature ALTERS the instrumented call's inputs or side effects, not just
reports on it.
**Cue:** "what does the agent add to / change about X".
**Example:** the SQL command wrapper writes back `CommandText` (the SQL-metadata
query comment) that the real database then executes.

### One-value-divergent-consumers
One value fans out to transformers that treat it OPPOSITELY.
**Cue:** "how is X handled" where X flows to more than one consumer.
**Example:** SQL parsing strips comments while SQL obfuscation passes them
through — same input string, opposite handling.

### One-of-many-winner selection
A selector picks ONE instance to report by a fixed precedence.
**Cue:** "which X gets reported" when several exist.
**Example:** the transaction's error-to-report is chosen by a fixed precedence,
not "the first" or "all".

### Origin-forks-on-upstream-state
A value has more than one origin depending on upstream state (root vs inherited,
cold vs warm, first vs subsequent).
**Cue:** "where does X come from" for a per-transaction value.
**Example:** a root transaction's priority/sampled are created locally; an
inherited transaction's come from the accepted trace context and may be
overridden by remote-parent behavior.

### Downstream-consequence
A classification CHANGES behavior elsewhere (metrics, apdex, sampling).
**Cue:** "what happens when X is classified as Y".
**Example:** an "expected" error still emits an error event and is excluded from
the error-rate metrics (recorded only under `ErrorsExpected/all`), but — unlike
an unexpected error — it is NOT forced to a frustrated apdex score; it is scored
normally on response time (`TransactionTransformer.GetApdexMetrics`,
`MetricWireModel.TryBuildErrorsMetrics`).

## EGRESS

### Second / parallel path
The same capability is served by more than one path.
**Cue:** "what / how does the agent send X".
**Example:** loaded assemblies ship BOTH in the one-time connect snapshot AND in
the periodic loaded-modules harvest.

### Snapshot-vs-incremental
One path sends a startup snapshot; another sends deltas afterward.
**Cue:** "what does the agent report about X over time".
**Example:** connect-time environment snapshot vs the incremental
loaded-modules harvest (seen-list reset on reconnect).

### Scope / destination filter
A value applies only to SOME payloads/destinations, not all.
**Cue:** "which payload carries attribute X".
**Example:** the attribute-definition `.AppliesTo(...)` set is the single source
of truth for which payloads carry an attribute (e.g. `queueDuration` →
transaction event + error event only).

### Non-default destination / transport
Data takes a non-default transport or destination.
**Cue:** "where does X go".
**Example:** serverless mode writes telemetry to a file/stdout instead of the
collector; infinite tracing streams spans over gRPC, not the HTTP collector;
bridged OpenTelemetry metrics go to an OTLP endpoint.

### Same-concept-renamed-key
One logical value is emitted under different keys to different destinations.
**Cue:** "what is X reported as".
**Example:** the Lambda ARN appears as both `metadata.arn` and `aws.lambda.arn`;
`cloud.resource_id` is emitted from multiple sources.

### Same-model-multiple-wire-shapes
One model is serialized differently per transport/mode.
**Cue:** "what does the X payload look like".
**Example:** a span event is a positional JSON array over HTTP but a named-field
protobuf over gRPC; `record_sql` is a bare string in one connect model and an
`{enabled}` dict in another.

### Request/response schema asymmetry
Negotiated config is sent under one key and returned under another.
**Cue:** "how is X negotiated with the collector".
**Example:** an event limit sent inside `event_harvest_config` is returned under
a separate `span_event_harvest_config`.

## CONTROL-PLANE / SELF

### Local-artifact write-triggers
A local artifact is written on MULTIPLE triggers, not one.
**Cue:** "when / how is the X file written".
**Example:** the agent-health (control) file is written by
`AgentHealthReporter.PublishAgentControlHealthCheck` on a recurring timer (first
write immediately on connect, then every `HealthFrequency` seconds) plus a final
write on shutdown; failures only update the in-memory `HealthCheck` status via
`SetAgentControlStatus` and don't themselves write the file. (Enumerate the real
triggers — "on failure" is the tempting wrong assumption here.)

### Target / best-effort-vs-guarantee
A quantity is a TARGET with overflow, not a hard cap (or vice versa).
**Cue:** "how many X / what's the limit".
**Example:** adaptive sampling's per-minute figure is a target with overflow
behavior, not a hard ceiling.

### Platform / TFM-conditional agent-self behavior
Behavior forks on the AGENT'S OWN platform — its build TFM (`#if NETFRAMEWORK`)
or the runtime CLR version — not on a monitored library's version.
**Cue:** ".NET Framework vs .NET" / "does it behave differently on X runtime".
**Example:** async context storage selection forks on the runtime .NET Framework
version (`AgentInstallConfiguration.IsNet46OrAbove`, applied in
`ExtensionsLoader`): the AsyncLocal storage extension is loaded only on .NET
4.6+, otherwise CallContext storage is used.

### Control-surface enumeration
"What are ALL the X" needs an exhaustive cross-subsystem list, not one example.
**Cue:** "list every X / all the ways to Y".
**Example:** "every control that keeps data in-process" spans HSM, LASP,
attribute include/exclude, record_sql, strip-exception-messages, etc.

### Decision-path-vs-reported-status
The code that DECIDES an outcome differs from the code that REPORTS it; they can
disagree.
**Cue:** "why does the agent say X when Y".
**Example:** a 401 is retried on one path yet surfaced as a generic
connection-failure status on another.

## SEMANTICS / CLAIM-SHAPE

### Per-property precedence (inverted / laddered / multi-tier)
Configuration precedence is PER-SETTING and may be inverted, laddered, or
multi-tier. **Never restate the `CLAUDE.md` "env > newrelic.config > server >
defaults" mantra without reading the getter** — and read the resolver HELPER it
calls, because the AND-vs-override and parsing semantics live there, not at the
call site.
**Cue:** "how is config setting X resolved / which wins".
**Failure shapes to look for:**
- `ServerOverrides(serverValue, EnvironmentOverrides(...))` nesting (often with a
  "Faster Event Harvest" comment) ⇒ **server beats env** — the inverse of the
  mantra (e.g. the span/transaction-event max-samples settings).
- A hardcoded ordered ladder where env is NOT first, or one env var beats
  another (the license key resolves AppSettings > env > config; the app name is
  a multi-step ladder where `IISEXPRESS_SITENAME` beats `NEW_RELIC_APP_NAME` and
  `newrelic.config` is last).
- A four-tier appSettings order (Env > XML attribute > appSettings > Default)
  the three-tier mantra cannot express.
- `ServerCanDisable` ANDs rather than overrides — the server can only turn a
  locally-enabled feature OFF, never back on.
**Bidirectional check:** confirm an env tier EXISTS before implying one — grep
the `SCREAMING_SNAKE` name; absence of an `EnvironmentOverrides` call at the
getter means there is no env var (some settings are config/XSD-only, e.g.
`excludeNewrelicHeader`; Agent Control settings are bootstrap/env-only with no
`newrelic.config` knob).

### Exclusive / asymmetric bound
A bound is exclusive, or a matching rule is asymmetric between two cases.
**Cue:** "is version X supported / does X match".
**Example:** `maxVersion` is strictly-less-than (exclusive); expected-error
status codes support ranges while ignored ones do not.

### Shared-term-different-subsystem
The key noun in the question maps to more than one unrelated mechanism;
disambiguate BEFORE tracing.
**Cue:** an overloaded term ("sampler", "log level", "harvest", "priority").
**Example:** "sampler" = the adaptive trace sampler vs host-metric samplers vs
the event-reservoir cap; "log level" = the agent's own diagnostic log vs the
forwarded application log.

### Negative / absence claims
The claim is that the agent does NOT do something; the evidence is the ABSENCE
of code.
**Cue:** "does it NOT do X / is X unsupported".
**Example:** ignored status codes support no ranges — proven by the absence of a
range-parse call, and stated as inference per the honesty rule.
