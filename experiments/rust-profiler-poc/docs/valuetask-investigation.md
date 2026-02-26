# ValueTask and IAsyncEnumerable Instrumentation Analysis

Investigation date: 2026-02-26

## Problem Statement

The current New Relic .NET agent cannot instrument methods returning `ValueTask` or
`IAsyncEnumerable` because the async instrumentation pipeline is built on the assumption
that async methods return `Task` or `Task<T>` (reference types). This is visible in
`Delegates.cs`:

```csharp
public static AfterWrappedMethodDelegate GetAsyncDelegateFor<T>(...) where T : Task
```

The `where T : Task` constraint excludes `ValueTask` (a struct). Individual wrappers
work around this by instrumenting callers instead of the ValueTask-returning method
itself.

## Why ValueTask Is Hard

1. **Struct, not class**: Can't satisfy `where T : Task`, boxing defeats its purpose
   (avoiding allocations on the synchronous completion fast path)
2. **Single-await rule**: Can only be awaited once; holding/re-awaiting causes undefined
   behavior
3. **IValueTaskSource backing**: When backed by a pooled `IValueTaskSource<T>`, the
   backing object may be reused after first await completes
4. **No ContinueWith**: Unlike Task, there's no `ContinueWith()` to attach callbacks;
   must await directly or convert via `.AsTask()` (which allocates)
5. **Boxing to object**: The profiler's `GetFinishTracerDelegate` passes return values
   as `object`. Boxing a ValueTask breaks the single-await contract and adds allocation

## Why IAsyncEnumerable Is Harder

1. **Stream, not single completion**: Multiple `MoveNextAsync()` calls, each returning
   `ValueTask<bool>`. No single "done" event to hook
2. **Iterator lifetime**: Starts at enumeration, ends at disposal; doesn't map to
   try/catch/finally pattern
3. **No APM tool handles it**: Zero implementations found across Datadog, Elastic,
   OpenTelemetry, or any other profiler-based .NET APM

## Industry Approaches

### Datadog (dd-trace-dotnet) — The Reference Implementation

Datadog has the most sophisticated ValueTask handling. Their architecture splits
responsibility:

**Native profiler (C++)**: IL rewriting inserts calls to generic managed handlers.
The profiler does NOT know about ValueTask — it treats all return types generically
via `EndMethodHandler<TIntegration, TTarget, TReturn>`.

**Managed agent (C#)**: Inspects `TReturn` at static constructor time:
- `Task` → `TaskContinuationGenerator`
- `ValueTask` → `ValueTaskContinuationGenerator`
- `ValueTask<T>` → `ValueTaskContinuationGenerator<T>`

**The key trick**: `Unsafe.As<TFrom, TTo>()` reinterprets between the generic
`TReturn` and `ValueTask<T>` without boxing:

```csharp
// Zero-allocation type conversion
protected static TReturn ToTReturn<TFrom>(TFrom returnValue)
    => Unsafe.As<TFrom, TReturn>(ref returnValue);

protected static TTo FromTReturn<TTo>(TReturn returnValue)
    => Unsafe.As<TReturn, TTo>(ref returnValue);
```

Then creates an async continuation:

```csharp
private async ValueTask<TResult> ContinuationAction(
    ValueTask<TResult> previousValueTask, TTarget? target, CallTargetState state)
{
    TResult result;
    try {
        result = await previousValueTask.ConfigureAwait(_preserveContext);
    } catch (Exception ex) {
        _continuation(target, default, ex, in state);
        throw;
    }
    _continuation(target, result, default, in state);
    return result;
}
```

The new ValueTask is reinterpreted back to `TReturn` via `ToTReturn()`.

**Sync fast path**: Checks `IsCompletedSuccessfully` before allocating a continuation.
Critical for performance since many ValueTask-returning methods complete synchronously.

**Key source files** (GitHub: DataDog/dd-trace-dotnet):
- `tracer/src/Datadog.Trace/ClrProfiler/CallTarget/Handlers/EndMethodHandler`1.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/CallTarget/Handlers/Continuations/ValueTaskContinuationGenerator.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/CallTarget/Handlers/Continuations/ValueTaskContinuationGenerator`1.cs`
- `tracer/src/Datadog.Trace/ClrProfiler/CallTarget/Handlers/ValueTaskHelper.cs`

### Elastic APM & OpenTelemetry

Both derived from Datadog's CallTarget approach. Same `Unsafe.As` trick, same
continuation pattern. Neither supports IAsyncEnumerable.

## Architecture Implications for This POC

### Option A: Box-and-Inspect (Minimal profiler change)

The current profiler approach (box return value to `object`, pass to finish delegate)
works for ValueTask with no profiler-level changes. The managed agent would:

1. Receive the boxed ValueTask as `object`
2. Detect the type via reflection
3. Unbox, create continuation, return new ValueTask

**Pros**: No profiler changes needed; current IL injection template works as-is
**Cons**: Boxing overhead on every call; violates ValueTask's design intent

### Option B: Generic CallTarget (Datadog's approach)

Redesign IL injection to preserve the return type generically:

```
// Instead of:
object result = originalMethod();
finishTracer(result);  // boxed

// Emit:
TReturn result = originalMethod();
EndMethodHandler<TIntegration, TTarget, TReturn>.Invoke(instance, result, exception, state);
```

**Pros**: Zero boxing; best performance; proven pattern (Datadog/Elastic/OTel all use it)
**Cons**: Requires different IL injection template; profiler must emit generic method
calls; significant architectural change

### Recommendation

**For this POC**: The current approach is fine. ValueTask support doesn't require
profiler-level changes. When we implement the agent bootstrap sequence, we should
keep Option B in mind as a future enhancement but not let it block the current work.

**For production**: Option B (generic CallTarget) is the industry-proven approach.
If we decide to adopt it, the profiler's IL injection template would change
significantly, but the instruction scanner, ret rewriter, EH manipulator, and other
infrastructure we've built would still be used. The change would be in
`inject_default.rs`'s template, not in the supporting IL infrastructure.

### IAsyncEnumerable

Deprioritize. No APM tool in the industry handles it at the profiler level. If needed
in the future, it would be handled at the managed wrapper level via a decorator pattern
around `IAsyncEnumerator<T>`, not via IL injection changes.

## Impact on Current POC Architecture

| Component | ValueTask (Option A) | ValueTask (Option B) | IAsyncEnumerable |
|-----------|---------------------|---------------------|-----------------|
| instruction_scanner.rs | No change | No change | No change |
| instruction_builder.rs | No change | Template changes | No change |
| inject_default.rs | No change | New template variant | No change |
| exception_handler.rs | No change | No change | No change |
| tokenizer.rs | No change | New generic tokens | No change |
| profiler_callback.rs | No change | Detect return type | No change |
| Managed agent | New continuation generators | New CallTarget handlers | Decorator pattern |

**Bottom line**: The IL infrastructure we've built is reusable regardless of which
approach is chosen. The decision point is in the managed agent architecture and the
IL injection template, not in the bytecode manipulation primitives.
