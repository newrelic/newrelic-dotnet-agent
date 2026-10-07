# Context Map

This repository has two bounded contexts with their own glossaries. Each
CONTEXT.md is a glossary for its own context; this map names the contexts and
reconciles the words they share.

## Contexts

- [Managed Agent](./CONTEXT.md) -- the C# agent core: transactions, segments,
  spans, wrappers, tracers, and the telemetry pipeline.
- [Native Profiler](./src/Agent/NewRelic/Profiler/CONTEXT.md) -- the C++
  CLR-profiling component: matches methods and rewrites their IL so calls route
  into the managed agent.

## Relationships

The profiler rewrites method IL so calls route into the managed agent; the
managed agent supplies the wrappers and tracers those rewritten calls invoke.
Several words appear in both contexts with related but distinct meanings --
resolve them by context:

- **Instrumentation**: Profiler = the concrete act of rewriting IL at
  JIT/ReJIT. Managed agent = the capability in the abstract, plus the XML that
  declares what to instrument. Same goal, different altitude.
- **Instrumentation point (profiler) vs instrumentation XML (managed)**: both
  derive from the same tracerFactory/match XML. The managed side authors and
  ships the XML; the profiler parses each entry into an instrumentation point
  used to match JIT'd methods.
- **Tracer / tracer factory**: the XML element is tracerFactory. Managed = it
  names a wrapper (a tracer is the legacy per-call handle). Profiler = it
  becomes an instrumentation point carrying tracer flags. Neither is a "trace".
- **TracerFlags**: one concept, two representations kept in sync -- TracerFlags
  in the profiler (C++) and TracerFlags in the managed agent (C#).
- **Custom vs live instrumentation**: the collector can push instrumentation at
  runtime. The profiler calls this "custom instrumentation"; the managed agent
  calls it "live instrumentation". Same feature.
- **Context-exclusive terms**: segment, span, tracer, and wrapper live only in
  the managed context; function, module, metadata token, and Sicily live only
  in the profiler context.
