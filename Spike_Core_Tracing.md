# [*Core Tracing*]

| Developer | Alex Hemsath  |
| :---- | :---- |
| **Date** | 2026-05-14 |
| **JIRA** | https://new-relic.atlassian.net/browse/NR-520524 |
| **Test Branch** | https://github.com/newrelic/newrelic-dotnet-agent/tree/poc/core-tracing |
| **Status of Test Branch** | Not yet started — investigation only |
| **Next Step** | Continue investigation; produce work breakdown for implementation |
| **Related Links** | Epic: [NR-538819](https://new-relic.atlassian.net/browse/NR-538819) · Parent Feature: [NR-543154](https://new-relic.atlassian.net/browse/NR-543154) · Initiative Brief: [Trace Granularity Control](https://newrelic.atlassian.net/wiki/spaces/TRACING/pages/3763044458) · Specs: [Span Events](https://source.datanerd.us/agents/agent-specs/blob/main/Span-Events.md), [Distributed Tracing](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md) · Linked Work Request: [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) |

# Goal

Bring the .NET agent to feature parity with the Core Tracing capabilities (officially called *Partial Granularity* in the agent specs[^dt-spec-pg], and branded *Trace Granularity Control* in the Initiative Brief[^brief]) that were in Limited Preview as of 2026-04-01[^epic-scope].

The work spans three loosely-coupled pieces in the DT spec, all of which the .NET agent currently lacks or only partially implements:

1. **Sampler configuration overhaul.** Replace the single hardcoded adaptive sampler with the spec's per-section sampler model: independent samplers for `root`, `remote_parent_sampled`, and `remote_parent_not_sampled`, each selectable from `adaptive`, `always_on`, `always_off`, or `trace_id_ratio_based`[^dt-spec-config]. Adaptive sampling target becomes user-configurable (1–120, default 10)[^dt-spec-adaptive], matching what Java already ships[^brief-java-tpm].
2. **Partial granularity (Core Tracing).** Implement `distributed_tracing.sampler.partial_granularity` with all three reduction modes[^dt-spec-pg-type]:
   - `reduced` — drop all non-entity-synthesis, non-LLM in-process spans; keep entry span and exit spans; preserve their attributes.
   - `essential` (spec default) — same span reduction as `reduced`, plus strip all non-entity-synthesis agent attributes and all custom attributes (errors and SpanLink intrinsics retained).
   - `compact` — same as `essential`, plus compress all spans sharing the same entity-synthesis attributes into one span, recording the merged ids/durations on `nr.ids` / `nr.durations` and re-parenting onto the entry span.
   Full-granularity sampling takes precedence over partial; full and partial can be enabled simultaneously[^dt-spec-precedence].
3. **Outbound header policy.** `distributed_tracing.exclude_newrelic_header` defaults to **true** in the current spec[^dt-spec-config] — outbound `newrelic` header is off by default and only W3C `traceparent`/`tracestate` are emitted[^dt-spec-outbound]. The .NET agent currently defaults this to `false`; flipping the default is a behavior change customers will notice and must be coordinated with release-notes / migration guidance.

Adjacent items called out by the Epic[^epic-scope] but not strictly part of partial granularity:

- **Configurable adaptive sampling target.** Falls out naturally from item (1) above.
- **Skip headers when not sampled.** Performance win on message-queue paths (Kafka)[^epic-scope].

# Customer Impact

- **Cost control.** Customers currently face a binary choice between full-detail DT (expensive) and no DT. Partial granularity gives them per-entity dials to keep critical services at full granularity while reducing volume on low-priority services.[^brief-opportunity]
- **Trace fragmentation.** ~90% of APM+ customers send some DT data, but only ~45% of instrumentable entities actually emit traces.[^brief-opportunity] Lightweight tracing is cheap enough to enable on every entity, eliminating gaps in T360 / Dynamic Flow Map / Workloads views.[^brief-opportunity]
- **New-customer onboarding.** A low-cost default lets new customers light up an entire service map without a full instrumentation investment, surfacing value before they commit to full DT cost.[^brief-why]
- **Header overhead.** Defaulting `exclude_newrelic_header` to true cuts outbound header size on every cross-service call; particularly important on Kafka and similar high-volume queue paths.[^epic-scope]
- **Sampling configurability.** Today .NET is locked at 10 traces/min[^brief-java-tpm]. Java's 120 tpm is a documented advantage for enterprise apps (rare-error capture, cascading-failure visibility, high-value transaction guarantees)[^brief-adaptive] — exposing the knob lets cost-sensitive customers turn it down and high-fidelity customers turn it up.[^brief-adaptive]
- **Known follow-on issue.** [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) — `TransactionError` events do not always have a corresponding span with matching error attributes, breaking Intelligent Exemplar lookups.[^nr-546778] The `essential` and `compact` modes intentionally keep error attributes after stripping everything else[^dt-spec-pg-type], so any LGT regression here would amplify the existing bug. The fix for NR-546778 (already closed) needs to be reverified under partial granularity before we ship.

# History

- **FY27Q1 .NET Agent Core Tracing** ([NR-543154](https://new-relic.atlassian.net/browse/NR-543154)) is the parent feature; this spike is the first ticket under it.
- The Initiative was originally pitched as "Core Tracing" / "DT Lite" and was renamed to **Trace Granularity Control** to reflect that it is a configuration of DT rather than a separate product[^brief]. The DT spec uses **partial granularity** as the canonical engineering term[^dt-spec-pg] — naming-in-flux note in the brief is real[^brief], and we should track which name lands in customer-facing docs before mirroring it into config keys.
- The first Core Tracing release on other agents surfaced [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) (`TransactionError` ↔ Span error mismatch)[^nr-546778], which is now closed but flagged on this Jira as a class of bug to watch for[^nr-520524-comment].
- Other agents are ahead of .NET[^epic-scope]:
  - **Java** — already ships the 1–120 tpm adaptive target[^brief-java-tpm]; leading on header de-duplication and skip-headers-when-not-sampled[^epic-scope].
  - **Python** — leading on `exclude_newrelic_header` true-by-default and W3C-only propagation[^epic-scope].
  - **PHP** — running its own Core Tracing scoping spike in parallel[^epic-scope].

# Findings

## Spec terminology vs. brief naming

Brief column is sourced from the Initiative Brief's "Vision" section[^brief-vision]; spec column is from the DT spec § Partial granularity type[^dt-spec-pg-type].

| Brief term | Spec term | Notes |
| :---- | :---- | :---- |
| Minimal Spans Tracing (MST) — "no in-process spans" | `partial_granularity.type = "reduced"` | Spans only; full attributes preserved. |
| Low-Granularity Tracing (LGT) | `partial_granularity.type = "essential"` (spec default) | Span reduction **and** attribute stripping. |
| MST — "no in-process + compress client spans" | `partial_granularity.type = "compact"` | Span reduction + attribute stripping + entity-bucket compression with `nr.ids` / `nr.durations`. |
| Two independent dials (MST + LGT) | One `type` enum with three values | Spec collapses the two-dial concept into a single ordered axis: `reduced` ⊂ `essential` ⊂ `compact`. There is no "drop spans but keep attributes on the survivors **and also** keep custom attributes" mode separate from `reduced` — `reduced` already keeps attributes. |

This means the customer-facing "two dials" framing in the brief maps to a **single config knob** at the agent level. Any UI that wants to expose them as two dials has to translate, but the agent config surface is one enum.

## Configuration shape (from DT spec § Configuration)

```yaml
distributed_tracing:
  enabled: true                        # already exists
  exclude_newrelic_header: true        # spec default true; .NET currently false → behavior change
  sampler:
    adaptive_sampling_target: 10       # NEW configurable; 1..120; default 10
    root:                              # sampler when trace originates here
      adaptive: { sampling_target: ... }
      # or always_on / always_off / trace_id_ratio_based: { ratio: 0..1 }
    remote_parent_sampled:             # sampler when upstream sampled=true
      ...
    remote_parent_not_sampled:         # sampler when upstream sampled=false
      ...
    full_granularity:
      enabled: true                    # default true
    partial_granularity:
      enabled: false                   # default false
      type: "essential"                # "reduced" | "essential" | "compact"
      root: ...
      remote_parent_sampled: ...
      remote_parent_not_sampled: ...
```

Each section defaults to `adaptive` if unspecified[^dt-spec-config]. Any individual section's `adaptive.sampling_target` overrides the global `adaptive_sampling_target`[^dt-spec-adaptive]. The connect response's `sampling_target` overrides the global default but does **not** override per-section overrides[^dt-spec-adaptive].

## Partial granularity — what each `type` actually does

All three modes share these baseline rules[^dt-spec-pg-type]:
- All spans without [entity-synthesis attributes](#entity-synthesis-attributes) are dropped from the span tree, **except** the entry span and LLM spans.
- When a span is dropped, the tree is preserved by re-parenting its children to its parent.
- `nr.pg = true` is added to the entry span (intrinsic).[^span-events-pg]
- `nr.transactionDuration` is added to the entry span only when async work pushed the transaction past the entry span's end.[^span-events-pg]
- `SpanLink` events on dropped spans are re-parented to the closest surviving ancestor (with the per-Span limit of 100 links[^span-events-spanlink-limits] and a `Supportability/dotnet/PartialGranularity/SpanLink/Dropped` metric on overflow[^dt-spec-pg-type]).
- All `SpanEvent` events are dropped.[^dt-spec-pg-type]

Then layered on top:

| Mode | Span reduction | Attribute stripping | Compression |
| :---- | :---- | :---- | :---- |
| `reduced` | ✅ | ❌ keeps all attributes on surviving spans | ❌ |
| `essential` (default) | ✅ | ✅ drops all non-entity-synthesis agent attributes (keeps error.\*) and all custom attributes; SpanLink keeps only intrinsics | ❌ |
| `compact` | ✅ | ✅ same as `essential` | ✅ groups surviving spans by entity-synthesis attribute values, sums durations into `nr.durations`, lists merged span ids in `nr.ids`, re-parents everything onto the entry span |

### Entity-synthesis attributes

The agent's set of attributes that drive entity synthesis (and thus are kept across all three modes) per the spec[^dt-spec-entity-synthesis]:

```
cloud.account.id, cloud.platform, cloud.region, cloud.resource_id,
db.instance, db.system,
http.url,
messaging.destination.name, messaging.system,
peer.hostname,
server.address, server.port
```

This list is not static — it tracks the [entity-definitions](https://github.com/newrelic/entity-definitions) repo[^dt-spec-entity-synthesis]. We need a single source of truth in the .NET agent for "is this an entity-synthesis attribute?" so adding a new cloud entity in the future doesn't silently break partial granularity.

### New intrinsics on Span events (Span-Events spec § Partial Granularity)[^span-events-pg]

| Attribute | Where | When |
| :---- | :---- | :---- |
| `nr.pg` (bool, always true) | Entry span only | Whenever a partial-granularity trace is emitted. |
| `nr.transactionDuration` (float, seconds) | Entry/root span only | Only when root span duration < transaction duration (async case). |
| `nr.ids` (string array) | Compressed exit spans | `compact` mode only. List of merged span GUIDs. May exceed attribute size limit → emit `Supportability/dotnet/PartialGranularity/NrIds/Dropped`. |
| `nr.durations` (float, seconds) | Compressed exit spans | `compact` mode only. Sum of merged spans' durations, with overlap handled per spec scenarios. |

## Sampling precedence (DT spec § Sampling precedence)[^dt-spec-precedence]

1. Run full-granularity sampler first.
2. If full-granularity sampled → emit full-granularity trace; partial granularity is **not** evaluated for this trace.
3. If full-granularity did not sample → run partial-granularity sampler.
4. Reservoirs prefer full-granularity over partial-granularity when over capacity.
5. Priority: full-granularity sampled traces get +2; partial-granularity sampled traces get +1[^dt-spec-precedence]. `always_on` uses fixed priorities (3 full, 2 partial); `always_off` uses 0.[^dt-spec-samplers]

Special case: if both full and partial granularity for a section use `trace_id_ratio_based`, the partial ratio must be set to `full_ratio + partial_ratio` to avoid the trivial-but-wrong outcome where partial never fires (because full already covered its band of trace IDs). Only applies when full granularity is enabled for that section.[^dt-spec-ratio-sum]

Partial granularity is **not** supported with infinite tracing and must be disabled when infinite tracing is on.[^dt-spec-pg-config]

## Outbound header behavior

DT spec table (Outbound Request Headers) for a W3C-capable agent (which .NET is)[^dt-spec-outbound]:

| Inbound | `exclude_newrelic_header` | Outbound |
| :---- | :---- | :---- |
| W3C, NR | true | W3C only |
| W3C | true | W3C only |
| NR | true | create W3C |
| W3C, NR | false | W3C, NR |
| W3C | false | W3C, create NR |
| NR | false | create W3C, NR |

Spec default is `true`. Need to verify .NET's current default (believed to be `false`) and stage the flip behind release-notes / a major-version bump.

## Adaptive sampler nuances worth preserving

All four points are from the DT spec § Adaptive Sampler[^dt-spec-adaptive]:

- Sampling decision should be made **lazily** — at end of transaction, on outbound payload creation, or on `IsSampled` API call — to avoid wasting `sampled=true` slots on transactions that will inherit a remote decision.
- When upstream is from a trusted account (Trusted Account ID match in `tracestate` or `newrelic` header), inherit the upstream decision and **do not** count it against the local sampling target.
- Throughput used for the sampling decision is the count of transactions **without** an inbound DT payload, not all transactions — keeping this distinction is what makes adaptive sampling actually adaptive.
- The exponential-backoff algorithm in the spec is the authoritative reference; the existing .NET implementation should already have it but needs an audit since this spike will rebuild surrounding code.

## SpanLink / SpanEvent (informational)

The Span-Events spec adds `SpanLink` and `SpanEvent` events[^span-events-spanlink][^span-events-spanevent], but as of December 2025 the only path to creating them is the OpenTelemetry Tracing API[^span-events-spanlink][^span-events-spanevent]. APM-native instrumentation does not generate them today. Partial granularity must still **handle them correctly** (re-parent on drop, drop with span on overflow) for the OTel-bridge case[^dt-spec-pg-type], but we don't need to add APIs for native creation in this spike's scope.

# Open Questions

- **Header default flip.** What is .NET's current default for `exclude_newrelic_header`? If `false`, do we flip to `true` in this work, or stage it behind a feature flag for one release? What did Java/Python do? (Spec default is true[^dt-spec-config]; Python is named in the Epic as leading on this[^epic-scope].)
- **Config naming.** The brief uses "Trace Granularity Control" / MST / LGT in customer-facing docs[^brief-vision]. The spec uses `distributed_tracing.sampler.partial_granularity.type`[^dt-spec-pg-type]. Do customer-facing config keys mirror the spec or the brief? Other agents' shipped config keys are the precedent.
- **Entity-synthesis attribute list source.** Should the .NET agent hard-code the list from the spec, or load it from a shared definitions resource? The spec explicitly says the list is subject to change with entity-definitions repo updates[^dt-spec-entity-synthesis].
- **Custom attributes opt-in.** The brief says LGT spans must not get custom attributes by default but customers can opt in[^brief-requirements]. The spec has no opt-in toggle — `essential` and `compact` simply drop them[^dt-spec-pg-type]. Is the opt-in a future enhancement, or do we need a fourth mode / config flag in this work?
- **Compact-mode grouping key.** The brief notes that grouping by `http.url` is too granular (different paths to the same host don't compress) and proposes deriving a host name[^brief-fast-follows]. The spec says "spans containing the same entity-synthesis agent attribute values" without further refinement[^dt-spec-pg-type]. Has any agent refined this beyond the spec, and did they put the refinement in agent code or in the collector?
- **Intelligent samplers.** Epic P3 ("equal-by-type sampling", "sample errors and slow transactions")[^epic-scope] is **not** covered by the current spec. Out of scope for this spike?
- **Concrete LP feature list.** "All features that are in LP as of April 1" is what the parent feature commits to[^epic-scope]. We need an explicit list from the DT team — something like a feature checklist confirming whether reservoir behavior changes, supportability metrics, the connect handshake fields for `sampling_target`, etc., are or are not in the LP scope.
- **Async transaction duration mitigation.** The brief proposes extending the entry-span duration to the latest in-process span end as a fast follow[^brief-fast-follows]. Spec gives us `nr.transactionDuration` as the mechanism instead[^span-events-pg]. Are we expected to ship the spec mechanism, the brief's mitigation, or both?

# Remaining Work

Anticipated areas, by code location. Will be turned into concrete tickets after the open questions above are resolved.

- **`src/Agent/NewRelic/Agent/Core/DistributedTracing/`**
  - Refactor sampler from a single instance into the per-section model (`root`, `remote_parent_sampled`, `remote_parent_not_sampled`).
  - Add four sampler implementations: adaptive (existing logic, surfaced behind an interface), always_on, always_off, trace_id_ratio_based (new).
  - Implement priority adjustments per spec (+2 full, +1 partial, fixed for always_on/off).
  - Special-case the trace-id-ratio sum when both full and partial use it.
  - Audit lazy-evaluation of `sampled` and the trusted-account-bypass path.

- **`src/Agent/NewRelic/Agent/Core/Configuration/`**
  - New config keys under `distributed_tracing.sampler.{full_granularity,partial_granularity}` and the per-section sampler structures. `Configuration.xsd` regen via `xsd2code` (license header restored — never hand-edited).
  - Default flip for `exclude_newrelic_header` (pending decision in Open Questions).

- **`src/Agent/NewRelic/Agent/Core/Spans/` (`SpanEventMaker` and friends)**
  - Add the partial-granularity post-processing pass: drop non-entity-synthesis spans (except entry + LLM), strip attributes per `type`, compress under `compact`, re-parent SpanLinks, drop SpanEvents, attach `nr.pg` / `nr.transactionDuration` / `nr.ids` / `nr.durations`.
  - Centralize the entity-synthesis attribute set so adding a new cloud entity later requires only one edit.
  - Wire reservoir-priority preference for full > partial.

- **`src/Agent/NewRelic/Agent/Core/Transactions/`**
  - Plumb the granularity decision through to segment finalization without leaking it into wrapper code (wrappers should remain unaware).

- **`src/Agent/NewRelic/Agent/Core/Errors/`**
  - Verify the [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) fix still holds when partial granularity strips attributes — error.\* attributes must survive `essential` and `compact`.

- **Supportability metrics**
  - `Supportability/dotnet/PartialGranularity/SpanLink/Dropped`
  - `Supportability/dotnet/PartialGranularity/NrIds/Dropped`
  - Anything else the spec adds — full audit needed.

- **`tests/Agent/IntegrationTests/`**
  - Per-mode coverage (`reduced`, `essential`, `compact`).
  - Cross-agent compatibility: .NET → Java/Python/Node and back, with and without the `newrelic` header.
  - Sampler permutations including the trace-id-ratio sum edge case.
  - Async transactions exercising `nr.transactionDuration`.
  - Wrapper projects under `src/.../Extensions/Providers/Wrapper/*` have **no unit tests by convention** — anything not exercisable through the integration suite needs to be lifted into `NewRelic.Agent.Extensions` helpers and covered there.

- **Infinite tracing interaction**
  - Force-disable partial granularity when infinite tracing is enabled; emit a warning log on config conflict.

# Remaining Work Breakdown

*[Delete this section unless the next step is "Ready to Implement". If more than 2 tasks are necessary, consider creating a Milestone Doc instead.]*

| Description | Expected Dev Days |
| :---- | :---- |
|  |  |
|  |  |

# Sources

[^brief]: Initiative Brief, *"Trace Granularity Control* — Initiative Brief (previously Core Tracing, DT Lite)"*, opening header note "Naming in flux, still to be discussed". https://newrelic.atlassian.net/wiki/spaces/TRACING/pages/3763044458
[^brief-opportunity]: Initiative Brief § "The Opportunity" — "90% of our APM+ customers send *some* tracing data ... only ~45% of instrumentable entities are actually sending traces" and the three "core problems" (high barrier to entry, pervasive trace fragmentation, inflexible cost management).
[^brief-vision]: Initiative Brief § "Vision / A Suite of Dials for Customer-Controlled Tracing" — defines Dial 1 (MST: All spans / No in-process / No in-process + compress client) and Dial 2 (LGT: All attributes / Minimal span attributes).
[^brief-why]: Initiative Brief § "Why and why now?" item 1 — "Accelerate new customer adoption ... low-cost, low-effort way to light up their entire service map".
[^brief-requirements]: Initiative Brief § "Requirements / Data and cost" item 5 — "By default, LGT spans **should not** have custom attributes automatically added ... Customers should be able to opt-into adding custom attributes to LGT spans".
[^brief-adaptive]: Initiative Brief § "Adaptive Sampling Configurability" — value of higher sampling (capture rare errors, investigate cascading failures, trace high-value transactions) and the proposed change to expose adaptive sampling rate as configurable.
[^brief-java-tpm]: Initiative Brief § "Adaptive Sampling Configurability" — "the trace origin service samples between 10 - 120 traces per minute depending on agent ... Currently, the Java agent is the only agent that sample at a rate of 120 traces per minute and all the other agents sample at 10 traces per minute".
[^brief-fast-follows]: Initiative Brief § "Improvements to consider as fast follows" — async-trace duration mitigations and the exit-span compaction grouping-key concern (`http.url` too granular).
[^epic-scope]: Epic [NR-538819](https://new-relic.atlassian.net/browse/NR-538819) § "Scope for FY27Q1" — P1 items including ".NET support for Core Tracing (all features that are in LP as of April 1)", outbound `newrelic` header off / W3C-only (Python, all agents), config to skip headers when not sampled (Java), W3C de-duplication (Python, Java), PHP spike. P3 covers intelligent samplers.
[^nr-520524-comment]: Comment on Jira [NR-520524](https://new-relic.atlassian.net/browse/NR-520524) by Christopher Hynes (2026-04-13): "Note the linked Work Request, which is an issue found with the first release of Core Tracing, and something we should keep in mind."
[^nr-546778]: Jira [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) — "[.NET agent] Fix inconsistency between TransactionError events and Span error attributes". Description: TransactionError entries missing from Spans table due to sampling or errors outside transaction context, breaking Intelligent Exemplar lookups. Status: Closed.
[^dt-spec-pg]: Distributed Tracing spec § "Full and Partial Granularity (Also known as Core Tracing or Intelligent Tracing)". `C:\workspace\agent-specs\distributed_tracing\Distributed-Tracing.md`.
[^dt-spec-config]: Distributed Tracing spec § "Configuration" — yaml structure showing `distributed_tracing.{enabled, exclude_newrelic_header (default true), sampler.{adaptive_sampling_target, root, remote_parent_sampled, remote_parent_not_sampled, full_granularity, partial_granularity}}`.
[^dt-spec-pg-config]: Distributed Tracing spec § "Order of precedence between distributed tracing enabled options" / partial-granularity table — defaults `full_granularity.enabled=true`, `partial_granularity.enabled=false`; "Partial granularity is not supported and **MUST** be disabled in infinite tracing mode."
[^dt-spec-pg-type]: Distributed Tracing spec § "Partial granularity type" — definitions of `reduced`, `essential` (default), and `compact`, including span-tree drop rules, attribute stripping rules, SpanLink re-parenting + drop semantics, SpanEvent drop, and `compact`-mode entity-synthesis grouping with `nr.ids` / `nr.durations`.
[^dt-spec-samplers]: Distributed Tracing spec § "Samplers" — Always On (priority 3 full / 2 partial), Always Off (priority 0), Trace ID Ratio Based (ratio 0..1), Adaptive (default).
[^dt-spec-adaptive]: Distributed Tracing spec § "Adaptive Sampler" — `adaptive_sampling_target` default 10, range [1,120]; per-section `adaptive.sampling_target` overrides global; trusted-account-bypass on header match; `decided_count_last` uses transactions without inbound payload; lazy sampling decision (end-of-transaction / outbound-payload / `IsSampled` API call); exponential-backoff algorithm; connect-response `sampling_target` overrides global default.
[^dt-spec-entity-synthesis]: Distributed Tracing spec § "Entity synthesis attributes" — list of 12 attributes (`cloud.account.id`, `cloud.platform`, `cloud.region`, `cloud.resource_id`, `db.instance`, `db.system`, `http.url`, `messaging.destination.name`, `messaging.system`, `peer.hostname`, `server.address`, `server.port`); explicit note that the list tracks the [entity-definitions](https://github.com/newrelic/entity-definitions) repo and "will need to be kept in sync".
[^dt-spec-precedence]: Distributed Tracing spec § "Sampling precedence between full and partial granularity" — full granularity decided first; if it samples, partial is not evaluated; reservoirs prefer full over partial; +2 to priority for sampled full granularity, +1 for sampled partial granularity.
[^dt-spec-ratio-sum]: Distributed Tracing spec § "Sampling full and partial granularity with Trace ID Ratio Sampler" — when both full and partial use `trace_id_ratio_based` and full is enabled, partial ratio MUST be set to `full_ratio + partial_ratio`.
[^dt-spec-outbound]: Distributed Tracing spec § "Outbound Requests / Expected Outbound Request Headers" — table of W3C-capable / non-capable agent behavior under `exclude_newrelic_header` true vs false.
[^span-events-pg]: Span Events spec § "Partial Granularity Distributed Tracing Attributes" — definitions, types, and placement rules for `nr.pg`, `nr.ids`, `nr.durations`, `nr.transactionDuration`; duration-overlap scenarios; error-attribute prioritization on compressed spans. `C:\workspace\agent-specs\Span-Events.md`.
[^span-events-spanlink]: Span Events spec § "SpanLink Events" — December 2025 note that OpenTelemetry Tracing API is currently the only path to creating SpanLinks; APM agents do not generate them via native instrumentation.
[^span-events-spanlink-limits]: Span Events spec § "SpanLink Event Limits" — Max SpanLink events per Span event = 100; `Supportability/{language}/SpanEvent/Links/Dropped` on overflow.
[^span-events-spanevent]: Span Events spec § "SpanEvent Events" — December 2025 note that OpenTelemetry Tracing API is currently the only path to creating SpanEvents.
