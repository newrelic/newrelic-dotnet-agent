---
id: 8
title: Wrapper skipped for lack of an active transaction
tier: verified
scope: managed
min_level: finest
precedence: 60
signatures:
  - "No transaction, skipping method"
keywords: [wrapper, transaction, segment]
verified_versions: "10.54.0"
---

**Symptom.** Instrumentation looks correct, the profiler rewrote the method, and
no segment or transaction appears for it. Common with custom instrumentation on
background work, message consumers, and timers.

**The line, FINEST only.** `No transaction, skipping method
MyNamespace.MyClass.MyMethod(System.String)`.

**Verdict.** A wrapper whose `IsTransactionRequired` is true is skipped when no
transaction is active at the call. The instrumented method still gets rewritten,
so the profiler log looks perfect while nothing is recorded. The fix is to
instrument an entry point that starts a transaction, or to have the customer wrap
the work with the public API.

**Limit.** Two other paths in the same method return without recording anything
and log nothing at all: the current segment being a leaf, and a detach that
leaves no valid transaction. When the FINEST line is absent you cannot separate
those from a wrapper that never ran.

Indirect evidence when FINEST is unavailable: `Instrumenting method:` present in
the profiler log, and no transaction or segment activity for that method in the
managed log. Word the verdict as consistent with the wrapper being skipped, and
say that the path logs nothing.

**Specificity limit.** This signature is common and usually benign: it fires in
nearly any application that does background or non-transactional work, so its
presence alone is not a finding. This playbook is load-bearing only when the
data the customer says is missing is transaction-scoped. When the missing data
is of one specific type, look first for a wrapper that was disabled outright
([playbook 10](10-wrapper-disabled-consecutive-exceptions.md)) before
concluding this one.

## Customer fix

Instrument an entry point that starts a transaction, rather than the method that
does the work. When there is no such entry point, as with a timer or a background
loop, wrap the work with the public API: call
`NewRelic.Api.Agent.NewRelic.SetTransactionName` inside a method marked with the
`[Transaction]` attribute, so a transaction exists while the instrumented method
runs.

## Next ask

A FINEST-level log covering one execution of the code path, and a description of
what invokes the method - a request, a timer, a queue consumer, or application
startup.
