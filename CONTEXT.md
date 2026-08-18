# New Relic .NET Agent

Domain glossary for the .NET APM agent. This file is a glossary and nothing
else -- it names concepts, it does not describe how they are implemented.

## Language

### Work and timing

**Transaction**:
One unit of work inside a single process -- a web request or a background
job. It is the root that contains the tree of segments, and it owns the
sampling decision for that work.
_Avoid_: request (too HTTP-specific), unit-of-work, activity

**Segment**:
A single timed node in a transaction's call tree (a method call, an external
call, a database query). Segments are the agent's internal timing primitive
and always exist while a transaction is running.
_Avoid_: node, frame, operation

### Tracing

A segment can be projected outward two independent ways: into a transaction
trace and into a span. Neither projection changes the segment; both are views
of it. Because "trace" alone is ambiguous, it is a forbidden term -- always
qualify it. A tracer (see Instrumentation and extensions) is a different
concept and is not any kind of trace.

**Span**:
The distributed-tracing projection of a segment -- the event emitted so a
segment can join a trace that spans processes. A span exists only when span
events are enabled; a segment without span events emitted is not a span.
_Avoid_: bare "trace", segment (a span is the outward view, not the node)

**Root span**:
The single parent-less span that represents the transaction itself -- the
entry point of the process's work. Distributed tracing requires exactly one
parent-less span per transaction, and this is it.
_Avoid_: entry span, top segment, faux span

**Transaction trace**:
The detailed segment tree of one transaction, captured as a sample (typically
a slow one) for the single-process waterfall view. Scoped to one process.
_Avoid_: bare "trace", trace details, transaction sample

**Distributed trace**:
The cross-process trace, identified by a trace id and assembled from the span
events of many transactions across many services.
_Avoid_: bare "trace", DT (spell it out in prose)

### Instrumentation and extensions

**Instrumentation**:
The mechanism by which the profiler rewrites JIT-compiled methods so their
calls route into the agent -- IL injection. "Instrument a method" means to
target it for this rewriting.
_Avoid_: hooking, patching, weaving

**Instrumentation XML**:
The declarative files that tell the profiler which methods to rewrite and
which wrapper to invoke for each. The recipe, not the mechanism.
_Avoid_: instrumentation set, config (it is not newrelic.config)

**Instrumented method**:
A specific method that instrumentation has targeted, so its calls are routed
through a wrapper.
_Avoid_: hooked method, wrapped method (the method is instrumented; the call
is wrapped)

**Wrapper**:
The managed code that runs when an instrumented method is called: it
recognizes the method, starts a segment, and finishes it when the call
returns. Thin by design -- interesting logic belongs in the
NewRelic.Agent.Extensions assembly.
_Avoid_: hook, handler, interceptor

**Extension**:
A pluggable unit the agent loads at runtime from the extensions directory --
a wrapper (managed DLL) together with its instrumentation XML. Hot-reloadable
without restarting the process.
_Avoid_: plugin, module

**NewRelic.Agent.Extensions**:
The shared-helper assembly that wrappers reference (parsing, reflection,
helper types). Despite the name it is NOT an extension: it ships no
instrumentation and is not loaded from the extensions directory.
_Avoid_: calling this assembly "an extension"

**Tracer**:
The legacy, low-level handle for one instrumented method call: the injected
bytecode creates it and calls Finish when the call returns. The wrapper model
superseded it for authoring instrumentation, but "tracer" survives at the
injected-bytecode boundary and in the tracerFactory XML element. Prefer
"wrapper" for new instrumentation work. A tracer is unrelated to any trace.
_Avoid_: using "tracer" for new wrapper work (say wrapper), tracer as a
synonym for trace

**Tracer factory**:
The tracerFactory element in instrumentation XML that names the wrapper to
invoke for the matched methods. Despite the name it does not, in modern usage,
produce tracers -- it selects a wrapper.
_Avoid_: reading "tracer factory" as a factory that makes tracers

### Providers

**Provider**:
Umbrella term for a pluggable implementation under the providers area. Two
families exist -- wrapper providers and context storage providers -- so bare
"provider" is ambiguous; say which.
_Avoid_: bare "provider"

**Wrapper provider**:
Another name for a wrapper, reflecting that wrappers live under the providers
area. Prefer "wrapper".
_Avoid_: using "provider" alone to mean this

**Context storage provider**:
A runtime mechanism that stores the current transaction so it follows code
execution across threads and async continuations (backed by thread-local,
async-local, HttpContext, and similar). Priority-ranked: the highest-priority
one that can serve at the moment wins.
_Avoid_: context, storage (alone), transaction context (that is the stored
thing, not the provider)
