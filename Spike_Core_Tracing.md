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

Bring the .NET agent to feature parity with the Core Tracing capabilities (officially called *Partial Granularity* in the [agent specs](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md), and branded *Trace Granularity Control* in the Initiative Brief) that were in Limited Preview as of 2026-04-01.

The work spans three loosely-coupled pieces in the DT spec, all of which the .NET agent currently lacks or only partially implements:

1. **Configurable adpative sampling target.** Extend the `adaptive` sampler configuration option to make the target user-configurable, with a range from 1-120 (the current default target is 10), per the [brief](https://newrelic.atlassian.net/wiki/spaces/TRACING/pages/3763044458/Trace+Granularity+Control+Initiative+Brief+previously+Core+Tracing+DT+Lite#Adaptive-Sampling-Configurability)
2. **Partial granularity (Core Tracing).** Implement `distributed_tracing.sampler.partial_granularity` ([spec](https://s  ource.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#partial-granularity-type)) with all three reduction modes:
   - `reduced` — drop all non-entity-synthesis, non-LLM in-process spans; keep entry span and exit spans; preserve their attributes.
   - `essential` (spec default) — same span reduction as `reduced`, plus strip all non-entity-synthesis agent attributes and all custom attributes (errors and SpanLink intrinsics retained).
   - `compact` — same as `essential`, plus compress all spans sharing the same entity-synthesis attributes into one span, recording the merged ids/durations on `nr.ids` / `nr.durations` and re-parenting onto the entry span.
   Full-granularity sampling takes precedence over partial; full and partial can be enabled simultaneously ([spec](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#sampling-precedence-between-full-and-partial-granularity)).
3. **Outbound header policy.** `distributed_tracing.exclude_newrelic_header` defaults to **true** in the [current spec](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#sampling-precedence-between-full-and-partial-granularity). The .NET agent currently defaults this to `false`; flipping the default is a behavior change customers will notice and must be coordinated with release-notes / migration guidance.

Adjacent items called out by the Epic but not strictly part of partial granularity:

- **Skip headers when not sampled.** Performance win on message-queue paths (Kafka). [DACI](https://newrelic.atlassian.net/wiki/spaces/APM/pages/5347835916/DACI+Minimizing+DT+header+size+for+Message+Queues)

# Customer Impact

- **Cost control.** Customers currently face a binary choice between full-detail DT (expensive) and no DT. Partial granularity gives them per-entity dials to keep critical services at full granularity while reducing volume on low-priority services.
- **Trace fragmentation.** ~90% of APM+ customers send some DT data, but only ~45% of instrumentable entities actually emit traces ([brief](https://newrelic.atlassian.net/wiki/spaces/TRACING/pages/3763044458/Trace+Granularity+Control+Initiative+Brief+previously+Core+Tracing+DT+Lite#The-Opportunity)). Lightweight tracing is cheap enough to enable on every entity, eliminating gaps in T360 / Dynamic Flow Map / Workloads views.
- **New-customer onboarding.** A low-cost default lets new customers light up an entire service map without a full instrumentation investment, surfacing value before they commit to full DT cost.
- **Header overhead.** Defaulting `exclude_newrelic_header` to true cuts outbound header size on every cross-service call; particularly important on Kafka and similar high-volume queue paths.
- **Sampling configurability.** Currently, the .NET agent's adaptive sampler target is hardcoded to 10 traces/min. The Java agent's 120 tpm is a documented advantage for enterprise apps (rare-error capture, cascading-failure visibility, high-value transaction guarantees) — exposing the knob lets cost-sensitive customers turn it down and high-fidelity customers turn it up. ([brief](https://newrelic.atlassian.net/wiki/spaces/TRACING/pages/3763044458/Trace+Granularity+Control+Initiative+Brief+previously+Core+Tracing+DT+Lite#Adaptive-Sampling-Configurability))
- **Known follow-on issue.** [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) — `TransactionError` events do not always have a corresponding span with matching error attributes, breaking Intelligent Exemplar lookups. The `essential` and `compact` modes intentionally keep error attributes after stripping everything else, so any low-granularity traciung regression here would amplify the existing bug. The fix for NR-546778 (already closed) needs to be reverified under partial granularity before we ship.

# History

- **FY27Q1 .NET Agent Core Tracing** ([NR-543154](https://new-relic.atlassian.net/browse/NR-543154)) is the parent feature; this spike is the first ticket under it.
- The initiative was originally pitched as "Core Tracing" / "DT Lite" and was renamed to **Trace Granularity Control** to reflect that it is a configuration of DT rather than a separate product. The DT spec uses **partial granularity** as the canonical engineering term — naming-in-flux note in the brief is real, and we should track which name lands in customer-facing docs before mirroring it into config keys.
- The first Core Tracing release on other agents surfaced [NR-546778](https://new-relic.atlassian.net/browse/NR-546778) (`TransactionError` ↔ Span error mismatch), which is now closed, but we need to watch out for this bug in our implementation.
- Other agents are ahead of .NET (and can/should be consulted regarding implementation details):
  - **Java** — already ships the 1–120 tpm adaptive sampling target, and is leading on header de-duplication and skipping/modifying tracing headers outbound from unsampled transactions.
  - **Python** — leading on `exclude_newrelic_header` true-by-default and W3C-only propagation.

# Findings

## Spec terminology vs. brief naming

Brief column is sourced from the Initiative Brief's ["Vision" section](https://newrelic.atlassian.net/wiki/spaces/TRACING/pages/3763044458/Trace+Granularity+Control+Initiative+Brief+previously+Core+Tracing+DT+Lite#Vision); spec column is from the DT spec § [Partial granularity type](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#partial-granularity-type).

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

Each section defaults to `adaptive` if unspecified. Any individual section's `adaptive.sampling_target` overrides the global `adaptive_sampling_target`. The connect response's `sampling_target` overrides the global default but does **not** override per-section overrides.

## Partial granularity — what each `type` actually does

Per the [spec](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#partial-granularity-type), all three modes share these baseline rules:
- All spans without [entity-synthesis attributes](#entity-synthesis-attributes) are dropped from the span tree, **except** the entry span and LLM spans.
- When a span is dropped, the tree is preserved by re-parenting its children to its parent.
- `nr.pg = true` is added to the entry span (intrinsic).
- `nr.transactionDuration` is added to the entry span only when async work pushed the transaction past the entry span's end.
- `SpanLink` events on dropped spans are re-parented to the closest surviving ancestor (with the per-Span limit of 100 links and a `Supportability/dotnet/PartialGranularity/SpanLink/Dropped` metric on overflow).
- All `SpanEvent` events are dropped.

Then layered on top:

| Mode | Span reduction | Attribute stripping | Compression |
| :---- | :---- | :---- | :---- |
| `reduced` | ✅ | ❌ keeps all attributes on surviving spans | ❌ |
| `essential` (default) | ✅ | ✅ drops all non-entity-synthesis agent attributes (keeps error.\*) and all custom attributes; SpanLink keeps only intrinsics | ❌ |
| `compact` | ✅ | ✅ same as `essential` | ✅ groups surviving spans by entity-synthesis attribute values, sums durations into `nr.durations`, lists merged span ids in `nr.ids`, re-parents everything onto the entry span |

### Entity-synthesis attributes

The agent's set of attributes that drive entity synthesis (and thus are kept across all three modes) per the [spec](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#entity-synthesis-attributes):

```
cloud.account.id, cloud.platform, cloud.region, cloud.resource_id,
db.instance, db.system,
http.url,
messaging.destination.name, messaging.system,
peer.hostname,
server.address, server.port
```

This list is not static — it tracks the [entity-definitions](https://github.com/newrelic/entity-definitions) repo. We need a single source of truth in the .NET agent for "is this an entity-synthesis attribute?" so adding a new cloud entity in the future doesn't silently break partial granularity. TBD how to do this, or if other agents are committing to this.

### New intrinsics on Span events (Span-Events spec § Partial Granularity)

| Attribute | Where | When |
| :---- | :---- | :---- |
| `nr.pg` (bool, always true) | Entry span only | Whenever a partial-granularity trace is emitted. |
| `nr.transactionDuration` (float, seconds) | Entry/root span only | Only when root span duration < transaction duration (async case). |
| `nr.ids` (string array) | Compressed exit spans | `compact` mode only. List of merged span GUIDs. May exceed attribute size limit → emit `Supportability/dotnet/PartialGranularity/NrIds/Dropped`. |
| `nr.durations` (float, seconds) | Compressed exit spans | `compact` mode only. Sum of merged spans' durations, with overlap handled per spec scenarios. |

## Sampling precedence (DT spec § Sampling precedence)

1. Run full-granularity sampler first.
2. If full-granularity sampled → emit full-granularity trace; partial granularity is **not** evaluated for this trace.
3. If full-granularity did not sample → run partial-granularity sampler.
4. Reservoirs prefer full-granularity over partial-granularity when over capacity.
5. Priority: full-granularity sampled traces get +2; partial-granularity sampled traces get +1. `always_on` uses fixed priorities (3 full, 2 partial); `always_off` uses 0.

Special case: if both full and partial granularity for a section use `trace_id_ratio_based`, the partial ratio must be set to `full_ratio + partial_ratio` to avoid the trivial-but-wrong outcome where partial never fires (because full already covered its band of trace IDs). Only applies when full granularity is enabled for that section.[spec](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#entity-synthesis-attributes)

Partial granularity is **not** supported with infinite tracing and must be disabled when infinite tracing is on.

## Outbound header behavior

Per [spec](https://source.datanerd.us/agents/agent-specs/blob/main/distributed_tracing/Distributed-Tracing.md#expected-outbound-request-headers):

| Inbound | `exclude_newrelic_header` | Outbound |
| :---- | :---- | :---- |
| W3C, NR | true | W3C only |
| W3C | true | W3C only |
| NR | true | create W3C |
| W3C, NR | false | W3C, NR |
| W3C | false | W3C, create NR |
| NR | false | create W3C, NR |

Spec default is `true`, the .NET agent currently defaults to false.  TBD whether a major version bump is required to update our default to true.  Spec review discussion included the info that all non-W3C-compliant agent versions are now EOL'ed.

## Adaptive sampler nuances worth preserving

All four points are from the DT spec regarding the adaptive sampler:

- Sampling decision should be made **lazily** — at end of transaction, on outbound payload creation, or on `IsSampled` API call — to avoid wasting `sampled=true` slots on transactions that will inherit a remote decision.
- When upstream is from a trusted account (Trusted Account ID match in `tracestate` or `newrelic` header), inherit the upstream decision and **do not** count it against the local sampling target.
- Throughput used for the sampling decision is the count of transactions **without** an inbound DT payload, not all transactions — keeping this distinction is what makes adaptive sampling actually adaptive.
- The exponential-backoff algorithm in the spec is the authoritative reference; the existing .NET implementation should already have it but needs an audit since this feature will rebuild surrounding code.

## SpanLink / SpanEvent (informational)

The Span-Events spec adds `SpanLink` and `SpanEvent` events, but as of December 2025 the only path to creating them is the OpenTelemetry Tracing API. APM-native instrumentation does not generate them today. Partial granularity must still **handle them correctly** (re-parent on drop, drop with span on overflow) for the OTel-bridge case, but we don't need to add APIs for native creation in this spike's scope.

# Open Questions

- **Header default flip.** Should we gate this on a major version agent release? What did Java and Python do?
- **Config naming.** The brief uses "Trace Granularity Control" / MST / LGT in customer-facing docs; The spec uses `distributed_tracing.sampler.partial_granularity.type`. Do customer-facing config keys mirror the spec or the brief? Other agents' shipped config keys are the precedent.
- **Custom attributes opt-in.** The brief says LGT spans must not get custom attributes by default but customers can opt in. The spec has no opt-in toggle — `essential` and `compact` simply drop them. Is the opt-in a future enhancement, or do we need a fourth mode / config flag in this work?
- **Compact-mode grouping key.** The brief notes that grouping by `http.url` is too granular (different paths to the same host don't compress) and proposes deriving a host name (in "fast follows" section). The spec says "spans containing the same entity-synthesis agent attribute values" without further refinement. Has any agent refined this beyond the spec, and did they put the refinement in agent code or in the collector?
- **Intelligent samplers.** Epic P3 ("equal-by-type sampling", "sample errors and slow transactions") is **not** covered by the current spec. Assume this is out of scope for this spike.
- **Concrete LP feature list.** "All features that are in limited preview as of April 1" is what the parent feature commits to. We need an explicit list from the DT team (or whomever).
- **Async transaction duration mitigation.** The brief proposes extending the entry-span duration to the latest in-process span end as a "fast follow". Spec gives us `nr.transactionDuration` as the mechanism instead. Are we expected to ship the spec mechanism, the brief's mitigation, or both?
- **Living Entity Synthesis attributes list** - How are other agents ensuring that their agents stay on top of a potentially changing list of attributes used for entity synthesis?

# Remaining Work

Anticipated areas, by code location. Will be turned into concrete tickets after the open questions above are resolved.

## Implementation

- **`src/Agent/NewRelic/Agent/Core/DistributedTracing/`**
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

- **Infinite tracing interaction**
  - Force-disable partial granularity when infinite tracing is enabled; emit a warning log on config conflict.

## Testing

### Unit testing

As much testing of the new features should be done in unit tests as possible.

Minimal testing in integration tests.

Some manual cross-agent compatibility testing should be considered.

Performance testing should be done to evaluate impact of new features.

# Remaining Work Breakdown

*[Delete this section unless the next step is "Ready to Implement". If more than 2 tasks are necessary, consider creating a Milestone Doc instead.]*

| Description | Expected Dev Days |
| :---- | :---- |
|  |  |
|  |  |
