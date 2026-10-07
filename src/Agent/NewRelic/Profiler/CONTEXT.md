# Native Profiler

Domain glossary for the native C++ profiler. It implements the CLR Profiling
API, watches JIT/ReJIT and module-load events, matches methods against
instrumentation rules, and rewrites their IL so calls route into the managed
agent. This file is a glossary and nothing else. Words shared with the managed
agent are reconciled in the root CONTEXT-MAP.md.

## Language

### Components and mechanism

**Profiler**:
The native component that implements the CLR Profiling API and rewrites method
IL at JIT/ReJIT time. Unqualified, "profiler" means this component.
_Avoid_: agent (that is the managed side), and do not confuse with the Thread
Profiler

**Thread Profiler**:
A separate, self-contained feature in the same binary that periodically
samples managed call stacks for diagnostics. It does no IL rewriting and is
unrelated to the profiler's instrumentation role.
_Avoid_: profiler (they share only a word)

**Instrumentation**:
Here, the concrete act of rewriting a method's IL so its calls route into the
managed agent, performed at JIT or ReJIT. The managed-agent context uses this
word more abstractly -- see CONTEXT-MAP.md.
_Avoid_: hooking, patching, weaving

**ReJIT**:
Asking the CLR to recompile an already-JIT-compiled method so the profiler can
supply rewritten IL. The main path for instrumenting methods discovered after
first JIT, and the primary path on CoreCLR.
_Avoid_: recompile (too generic), re-instrument

### Method and module model

**Function**:
The profiler's concrete model of one JIT-observed method -- its module, type,
name, signature, and computed tracer flags. Backed by the CLR Profiling API.
_Avoid_: method (reserve for the CLR method being modeled)

**IFunction**:
The interface, mirroring Function's surface, that the rewriting engine depends
on so it never touches the CLR Profiling API directly. Function is its one real
implementation.
_Avoid_: treating IFunction and Function as different concepts -- one concept,
split for decoupling

**Module**:
The profiler's concrete model of one loaded CLR module, including the
metadata-writing operations used to inject helpers. Backed by the CLR Profiling
API.
_Avoid_: assembly (a module is not an assembly)

**IModule**:
The interface the ModuleInjector depends on; Module is its one real
implementation. The same interface/implementation split as IFunction/Function,
one level up.
_Avoid_: treating IModule and Module as separate concepts

**Tiny vs Fat method header**:
The two ECMA-335 encodings of an IL method body's header. Tiny holds only a
code size (no locals, no exception clauses); Fat carries max-stack, a locals
token, and exception-handling flags. A Tiny method must be promoted to Fat
before it can be wrapped in a try/catch.
_Avoid_: header (unqualified)

### Rewriting engine

**MethodRewriter**:
The orchestrator that pre-filters candidate methods by assembly/type/name and
applies the instrumentors in order (Helper, then Api, then Default) to decide
whether and how to rewrite each one.
_Avoid_: rewriter (unqualified)

**Instrumentor**:
A strategy that decides whether and how to instrument a given function. Three
exist: Default (ordinary matched methods), Api (the public API shim methods),
Helper (the profiler's own injected mscorlib helpers).
_Avoid_: manipulator (that is the layer below), instrumenter

**FunctionManipulator**:
A strategy that performs the actual IL rewrite of one method body. Its three
variants pair with the instrumentors: Instrument (user/framework methods), Api,
and Helper.
_Avoid_: instrumentor (that decides; the manipulator does the writing)

**InstructionSet**:
A string-based mini-assembler used during a rewrite: callers append opcodes and
labels as text, and it resolves jumps and emits the final IL byte buffer.
_Avoid_: bytecode buffer, instruction list

### Matching rules and configuration

**Instrumentation point**:
One parsed rule from instrumentation XML (a tracerFactory/match/method-matcher
entry): the assembly, class, method, optional version range, and tracer flags
that identify a method to rewrite and how.
_Avoid_: match, rule (be specific), instrumentation (the act, not the rule)

**InstrumentationConfiguration**:
The queryable set of all instrumentation points from every XML file; answers
"does this method match a rule?" during instrumentation.
_Avoid_: config (it is not newrelic.config)

**TracerFlags**:
A per-instrumentation-point bitmask (kept in sync with the managed TracerFlags)
describing behavior such as async, web vs other transaction, metric generation,
and transaction-tracer segment.
_Avoid_: options, settings

**Custom instrumentation**:
Instrumentation points pushed from the New Relic collector at runtime rather
than shipped as static XML files. The managed agent calls its equivalent "live
instrumentation" -- see CONTEXT-MAP.md.
_Avoid_: dynamic instrumentation

**Ignore instrumentation**:
An assembly (optionally class) pattern that suppresses rewriting regardless of
any matching instrumentation point.
_Avoid_: exclusion list, denylist (be specific)

### Metadata and signatures

**Metadata token**:
A CLR handle identifying a type, method, member, or string within a module's
metadata (TypeRef, TypeDef, TypeSpec, MemberRef, MethodSpec, and so on). The
currency of IL rewriting.
_Avoid_: handle, id

**Signature (blob)**:
An ECMA-335 encoded description of a method or type -- calling convention,
generic arity, return and parameter types -- stored as a compressed byte blob.
_Avoid_: prototype

**Token resolver**:
The role (ITokenResolver) that takes an existing metadata token and resolves it
back to a type-name string. The inverse of a tokenizer.
_Avoid_: tokenizer (that mints; a resolver reads)

**Tokenizer**:
The role (ITokenizer) that takes a type description and mints a new metadata
token for it. The inverse of a token resolver.
_Avoid_: resolver (that reads; a tokenizer mints)

### Sicily

**Sicily**:
The project's own CIL-assembler mini-language: it turns a short textual type or
method signature (for example, "class [mscorlib]System.String") into a metadata
token or raw signature bytes, so rewriting code can express targets as readable
string literals.
_Avoid_: parser, DSL (name it Sicily)

**Sicily Type vs signature Type**:
Two separate hierarchies, both named Type with a nested Kind. The Sicily AST
Type is the parsed input to minting a token; the signature Type is the decoded
output of reading an existing signature blob. They run in inverse directions --
do not conflate.
_Avoid_: using "Type" unqualified when both are in scope

### Helper injection

**ModuleInjector**:
The step that, at module load, injects agent helper methods into every module:
full method bodies into mscorlib, and references to those methods into every
other module.
_Avoid_: method rewriting (that transforms existing methods; this adds new
ones)

**Injected helper methods**:
The small managed methods the profiler adds (hosted on
System.CannotUnloadAppDomainException in mscorlib) that rewritten IL calls to
load assemblies/types/methods by reflection and to stash a MethodInfo in
per-AppDomain or thread-local storage.
_Avoid_: shim, stub

Note on "inject": the ModuleInjector injects whole methods at module load; the
rewriting engine injects IL instructions into a single method body. Same word,
two granularities.

### Thread Profiler

**StackWalk**:
One thread's captured call stack at a sampling instant -- a preallocated array
of stack frames.
_Avoid_: stack trace

**ThreadProfile**:
The per-thread record produced on each sampling pass: the managed thread id, an
error code, and its StackWalk.
_Avoid_: profile (unqualified)

**Profile**:
The collection of all ThreadProfiles for every live managed thread at one
sampling pass.
_Avoid_: using it for the Thread Profiler feature as a whole

**NameCache**:
A cache from function ids to preallocated type- and method-name buffers, so the
lock-free, non-allocating stack-walk snapshot never needs to allocate or lock.
_Avoid_: symbol cache
