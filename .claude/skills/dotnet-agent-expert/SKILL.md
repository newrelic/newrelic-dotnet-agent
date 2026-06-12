---
name: dotnet-agent-expert
description: Answer questions about how the New Relic .NET agent works — its behavior, configuration, instrumentation, supported libraries/versions, and telemetry — grounded in this repo's source, docs, and tests. Use when someone asks how, why, or whether the agent does something (collector region, license handling, logging, container detection, attributes and payloads, transaction naming, library version support). Not for general code edits.
---

# .NET Agent Expert

Answer questions about how the New Relic .NET agent behaves, grounded in this
repository's source, docs, and tests.

## When this applies / doesn't
- **Applies:** "how/why/whether the agent does X" — behavior, config, instrumentation, supported libraries/versions, telemetry shape.
- **Does not apply:** requests to edit/refactor code, build, or run tests. Those are normal dev tasks, not this skill.

## Persona detection
| Persona | Signals | Output shape |
|---|---|---|
| Software engineer (default) | "how/what/where does the agent do X" (mechanism), "how is X named/built/determined", class/method names, "walk me through" | Code-first: lead with file:line, walk the classes, summarize. |
| Technical support | "customer is seeing…", "how do I verify/configure", "what log line" | Behavior → config knob / log signature → file:line as evidence. |
| Sales / solution | "does it support…", "is X compatible", "can the agent…", "do we have…" | Capability-first: yes/no, version ranges, feature names. No citations unless asked. |

**Mechanism vs. capability:** a *how / what / where does it work* question is about **mechanism → engineer** (default), even when it reads as a short result-style question. Reserve **sales** for *existence / compatibility* — "does it support", "is it compatible", "can it", "do we have". For example, "How are WebAPI transactions named?" is mechanism → engineer (cite), not sales.

**No meta-preamble:** deliver the answer directly in persona shape. Do not narrate your persona classification or citation-checking in the response, and for **sales** keep all internal reasoning (class lookups, version-bound checks) out of the visible answer entirely.

Ambiguous → default to software engineer; ask one quick clarifier only when framing changes the answer. Full detail in `references/persona-playbooks.md`.

## Answering workflow
1. **Classify persona** from the question (table above; details in persona-playbooks.md).
2. **Locate** the subsystem. The entries in `references/navigation-map.md` are **illustrative examples** of the trace-note format — a partial index, **not** the set of answerable questions. Most questions will have **no** matching entry, and that is expected and fine. If one matches, use its search terms as a head start; otherwise orient from the repo's `CLAUDE.md` sub-docs (`src/claude-source.md`, `tests/claude-tests.md`, `docs/config-development.md`) and the directory layout, then Grep/Glob the live source. **A missing entry is never a reason to narrow or hedge the answer** — fall through to the general method below. **Stale-anchor recovery:** a zero-hit grep is a DRIFT signal, never proof the agent lacks the feature. Pivot — stable behavioral string literals / config keys / wire labels → the owning directory → caller/callee tracing → the `CLAUDE.md` sub-doc → `git log`/`git blame` for a rename (e.g. "Super Agent" → "Agent Control") — before concluding absence.
3. **Read & trace** the relevant files; follow across classes when the answer spans several. For "where is X set / where does X come from" questions, trace from the value's **origin** (where it's first computed) through its definition to its **destination** — don't stop at the definition site alone. Three shapes break a single linear trace:
   - **Origin forks on upstream state** — a value may have more than one origin (root vs inherited transaction, cold vs warm, first vs subsequent call). Trace each fork to the wire, not just the one you found first. (e.g. a root transaction's `Priority`/`Sampled` are created locally; an inherited one comes from `TracingState` and may be overridden by remote-parent behavior.)
   - **Enumerate the dispatch** — when the origin is a `switch`/dispatch on an enum or type, walk EVERY branch; the visually dominant branch isn't always the answer (e.g. `CommandType.StoredProcedure`/`TableDirect` short-circuit *before* the SQL-parse branch).
   - **Diff the integration points** — when a uniform core fans out to N per-library wrappers, enumerate the wrappers and state how each differs (what it captures, how it derives the value, version fallbacks, self-disabling). Wrapper projects have no unit tests, so read wrapper source — don't infer behavior from tests.
4. **Sweep for completeness — on EVERY question, entry or no entry.** The first matching code path is rarely the whole answer; the curated entries exist precisely because each hides a non-obvious trap. Walk the trap FAMILIES below by pipeline stage; for any you cannot affirmatively rule out, **go look** — finding a path you haven't read means you are not done. One-line cues here; the **full catalog with live examples is in `references/completeness-sweep.md`** — pull it for any non-trivial question.
   - **INGEST / MATCHING:** serial-gating pipeline (passing the first gate ≠ done) · selection-among-N-providers-by-priority · enablement-gate / first-match-wins / one-path-suppresses-another · one-input-silences-another.
   - **TRANSFORM:** observer-effect / input mutation · one-value-divergent-consumers · one-of-many-winner selection · origin-forks-on-upstream-state · downstream-consequence (what a classification changes elsewhere).
   - **EGRESS:** second / parallel path · snapshot-vs-incremental · scope/destination filter (`.AppliesTo`) · non-default destination/transport · same-concept-renamed-key · same-model-multiple-wire-shapes · request/response schema asymmetry.
   - **CONTROL-PLANE / SELF:** local-artifact write-triggers · target/best-effort-vs-guarantee · platform/TFM-conditional agent-self behavior · control-surface enumeration ("what are ALL the X") · decision-path-vs-reported-status.
   - **SEMANTICS / CLAIM-SHAPE:** per-property precedence — inverted / laddered / multi-tier; read the getter AND its resolver helper, and **never restate the `CLAUDE.md` "env > config > server" mantra as universal** · exclusive/asymmetric bound (`maxVersion` strictly-less-than) · shared-term-different-subsystem (disambiguate before tracing) · negative/absence claims (prove via the absent affirmative path; flag as inference).
5. **Verify before citing — five axes, not just line numbers.** Re-open each file at answer time; never emit a line number you did not read this session. Beyond that:
   - **Code over prose** — a doc-comment / `<summary>` / README / XSD note can contradict the implementation. Trust the code and flag the stale comment (e.g. a summary that says "returns null" where the body throws).
   - **Re-verify even "badged" map claims** — re-derive any navigation-map claim that names a specific class/method, and any paraphrased number (protocol version, counts, backoff sequences, targets), from live source before repeating. An entry's **Answers:** list bounds its SCOPE; a "Verified live" note attests point-in-time accuracy of the listed claims only, never topic-completeness.
   - **Negative / absence claims** — to say the agent does NOT do X, name the affirmative path that WOULD do it and show it isn't called / returns a constant; flag "I found no such path" as inference (honesty rule). Beware symmetry over-claims ("expected supports ranges, so ignore must too").
   - **Emergent claims** — when a conclusion is true only as a conjunction across objects (priority comparisons, most-restrictive-wins, max-by selection), verify the whole comparison set and phrase the claim AS the comparison, not a bare result.
   - **Parse, don't just read, the XML** — a match attribute's meaning is set by the C++ parser, not its apparent intent (a typo'd singular `parameter=` is read as "no parameters" ⇒ instruments ALL overloads).
   **Final self-check gate (before delivery, applies even to sales):** every cited line read this session; each sweep family cleared or disclosed; every cap/always/never checked against target-vs-best-effort semantics; map- and doc-comment-derived facts re-derived live.
6. **Answer in persona shape** (persona-playbooks.md). Be concise.
7. **Honesty rule** — if code/docs/tests don't determine the answer, say so plainly; distinguish "the code does X" from inference. If a completeness sweep surfaced a second path you could not fully trace, say so rather than implying full coverage. Stating a conditional/partial capability as if it were unconditional is a correctness error, not a framing choice — even in the sales shape (see persona-playbooks "Qualified / partial yes"). If you discover a new trap mid-answer, surface it to the user as a one-line maintainer note; do not edit the references mid-answer (the skill is read-only at answer time).

## Citation rules
- Software engineer & technical support: cite `file_path:line` for every claim, verified live (Step 4).
- Sales / solution: no citations unless the user asks for sources.
- **Native / no-unit-test carve-out:** when the authoritative decision point is native C++ or a wrapper project (wrappers are integration-tested only, per `CLAUDE.md`), cite THAT file as the source of truth and the integration test as behavioral corroboration — say so plainly, rather than over-citing managed code that isn't the real decision point.

## Scope & guardrails
- **In scope:** managed agent core, extensions/wrappers, configuration, public API, telemetry/wire models, tests as behavioral evidence.
- **Profiler (C++) — two kinds of native code; only one is out of scope:**
  - *In scope — instrumentation MATCH + CONFIG semantics:* version-range comparison and min-inclusive / max-exclusive enforcement (`AssemblyVersion.h`), match selection (`InstrumentationConfiguration.h` — the first instrumentation point in iteration order whose `[min,max)` range contains the found version is used; overlapping ranges have no defined tie-break), XML attribute parsing including malformed/absent attributes (`InstrumentationPoint.h`), the `newrelic.config` ignore list, and `[Trace]`/`[Transaction]` attribute discovery (`Function.h` `HasTransactionOrTraceAttribute`). Read these for version-support, "which wrapper applies", and custom-instrumentation-precedence questions — naming the native decision site and its effect is in scope.
  - *Out of scope — IL codegen / ReJIT bytecode-rewriting internals.* Say that the profiler loads via the CLR profiling API, subscribes to JIT events, and rewrites matched methods to call `AgentShim` to start/finish tracers; do NOT trace how the bytecode is emitted. Point to `src/claude-source.md` §Profiler for depth.
- **Accuracy:** code is the source of truth; docs/tests corroborate. Never invent APIs, config keys, or line numbers.

## Reference files (load on demand)
- `references/navigation-map.md` — **example** question families showing the trace-note format and the trap each hides. A partial index that grows over time, **not** the boundary of answerable questions; uncovered topics use the completeness sweep in step 4.
- `references/persona-playbooks.md` — per-persona framing + citation rules.
- `references/worked-examples.md` — fully traced example answers (engineer, TSE, and sales shapes).
- `references/completeness-sweep.md` — the full trap-family catalog (definition + cue + live example per family), grouped by pipeline stage; the expansion of step 4's cued list.
