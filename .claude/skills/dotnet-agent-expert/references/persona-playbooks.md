# Persona Playbooks

Per-persona framing, vocabulary, and citation rules. Detect the persona from
the question's wording, then shape the answer accordingly. Citations, when
used, are `file_path:line` verified live at answer time (never copied from
memory or from these examples).

## Software engineer (default)
**Detection signals:** "how is X implemented", "how/what/where does the agent do X" (mechanism), "how is X named/built/determined/structured", "where in the code", a class or method name, "internals", "walk me through", "what calls what".
**Answer structure:** Lead with the `file:line` of the decision point, walk the relevant classes in execution order, then close with a one-line summary of the behavior.
**Vocabulary / tone:** Precise and code-centric — class names, method names, enum values, field names. Assume the reader will open the files.
**Citations:** Always.
**Example answer (abridged):**
> The collector host is chosen in `ConnectionInfo.GetCollectorHost` (`src/.../DataTransport/ConnectionInfo.cs:60`). An explicit `CollectorHost` wins first (`:67`); otherwise the license key is matched against `accountRegionRegex` `^.+?x` (`:74`), the region prefix is spliced into `collector.nr-data.net` (`:78`), and a non-region key falls back to `collector.newrelic.com` (`:85`). Summary: region is derived from the license-key prefix unless `host` is set explicitly.

## Technical support
**Detection signals:** "customer is seeing…", "how do I troubleshoot/verify/configure", "why isn't X working", "what log line", "is this expected", "where do I check".
**Answer structure:** Behavior first (what the agent does), then the config knob or log signature to check, then `file:line` citations as supporting evidence at the end.
**Vocabulary / tone:** Practical and diagnostic — name the config attribute / env var, the log level, and the exact string to look for. Frame it as "to verify, check X."
**Citations:** Always (placed as evidence after the actionable guidance).
**Example answer (abridged):**
> If a customer's data is landing in the wrong region, the agent auto-selects the collector from the license-key prefix — an `eu01x…` key resolves to `collector.eu01.nr-data.net`, a plain key to `collector.newrelic.com`. To verify EU routing, confirm the license key has the region prefix, or set `service.host` (`NEW_RELIC_HOST`) explicitly, which always overrides. Evidence: `ConnectionInfo.GetCollectorHost` (`src/.../DataTransport/ConnectionInfo.cs:74`, `:78`, `:85`).

## Sales / solution
**Detection signals:** "does it support…", "is X compatible", "can the agent…", "do we have…", pre-sales or competitive framing, data-residency / capability questions. Sales is about the *existence or compatibility* of a capability — **not its mechanism**. A "how / what / where does it work" question is engineer, not sales, even when it reads as a quick yes/no.
**Answer structure:** Direct yes/no first, then the version ranges / feature names that back it up. Offer to point to sources only if the user asks.
**Vocabulary / tone:** Capability-framed and customer-facing — supported libraries, version ranges, feature names. Avoid class/method jargon and line numbers.
**MUST NOT:** **The first sentence must be the verdict** (a yes/no — possibly *qualified*, see "Qualified / partial yes" below) — no "I verified…", no "I now have the full picture", no "the instrumentation caps at…". Never lead with, or include anywhere in the answer (including any preamble), class names, file paths, line numbers, or raw XML-attribute jargon (`maxVersion="6.8.1"`). Keep all internal reasoning (persona detection, version-bound lookups, live-navigation narration) out of the visible answer. When you state a version ceiling, translate it to customer language and remember `maxVersion` is **EXCLUSIVE** (strictly less than): a `maxVersion="6.8.1"` bound means "up to but **not** including 6.8.1." Never invent a version floor or ceiling that isn't present in the instrumentation XML.
**Citations:** Off unless the user explicitly asks for sources.
**Example answer (abridged):**
> Yes — the agent supports MongoDB.Driver 3.7. MongoDB.Driver instrumentation covers roughly 2.3 through the latest tested 3.x (verified to 3.9), and the agent automatically selects the right instrumentation at the driver's 3.0 packaging boundary, so 3.7 is fully covered with no upper version cap. (Note this is the MongoDB.Driver NuGet package version, not the MongoDB server version.)

**Worked contrast — version bounds in customer language.** When live navigation
turns up a `maxVersion="6.8.1"` bound on the RabbitMQ instrumentation, do **not**
surface the raw attribute or narrate the lookup. Convert it to a customer-facing
range and open with the answer:
>
> ❌ **Wrong** (leads with a verification monologue and raw XML jargon):
> "I now have the full picture. The instrumentation matches cap at `maxVersion="6.8.1"` (exclusive)… so yes, we support RabbitMQ."
>
> ✅ **Right** (first sentence is the yes/no; version range in plain terms):
> "Yes — you can tell the prospect we support RabbitMQ. We instrument the RabbitMQ.Client library up to (but not including) 6.8.1, capturing the broker host and port, the queue and routing key, and producer/consumer spans, with distributed tracing carried across the queue via the message headers. (I can share exact supported-version details and sources if useful.)"

**Qualified / partial yes.** Not every capability is binary. When support is
partial, gated, vendor/host-specific, or default-on-but-toggleable, the first
sentence is STILL the verdict — but it may be a *qualified* verdict ("Yes, for
MSSQL and MySQL"; "Yes, behind a feature flag"; "Yes, for isolated-worker Azure
Functions"), followed by exactly ONE plain-language caveat carrying the
applicability-determining distinction (the vendor/host sub-case, the known
limitation, the default state, the cap or sampling). Keep it in customer
language — no class names, file paths, or raw config jargon. Stating a
conditional capability as if it were unconditional is a CORRECTNESS error, not a
tone choice: brevity never overrides accuracy, and a real limitation must not be
suppressed to honor "no internal reasoning." Before saying yes to a NAMED
sub-feature (e.g. explain plans, a specific attribute), confirm that sub-feature
exists for the exact vendor/version/host asked about — being instrumented does
not mean every sub-feature is available. For security-posture questions ("can it
leak PII", "what does HSM guarantee"), state the DEFAULT state plus the override
caveat (e.g. `record_sql` is raw by default) rather than a bare reassurance.

## Disambiguation
When the framing is ambiguous, default to **software engineer**. Ask one quick
clarifying question only when the framing genuinely changes the answer — i.e.
the depth of detail or whether to include citations would differ (for example,
"does it support RabbitMQ?" could be a sales capability check or an engineer
asking which attributes are emitted). Otherwise, answer directly in the
default shape.

A "how does the agent do X" mechanism question is **not** ambiguous — it is
engineer by default, even when the answer is a short result. "How are WebAPI
transactions named?" is mechanism (it asks how the name is built) → engineer
shape with citations, *not* a sales capability answer. Sales is only for
existence/compatibility framing ("does it support", "is it compatible").
