---
id: 1
title: Profiler never loaded
tier: verified
scope: profiler
min_level: error
precedence: 10
signatures:
  - "Error initializing CLR profiler info"
  - "or greater required. Profiler not attaching"
  - "The global newrelic.config file was not found at"
keywords: [profiler, load failure, environment variables, bitness]
verified_versions: "10.54.0"
---

**Symptom.** No data at all, and no managed agent log.

**Read the absence first.** No profiler log file at all means the CLR never
loaded the profiler DLL. `Profiler initialized` present means the profiler
loaded, so move to another playbook.

**Verdict.** Absent log means the CLR never called the profiler. The usual causes
are a missing or mistyped `*_PROFILER_PATH`, a GUID mismatch, a bitness
mismatch, or missing native dependencies. None of those can write a log, which is
why the absence is the finding.

**Limit.** The absence proves only that the profiler did not run. It does not
say which of those causes applies.

## Customer fix

Check the profiling environment of the process. `COR_ENABLE_PROFILING=1` and
`COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}` for .NET Framework, or
`CORECLR_ENABLE_PROFILING=1` and
`CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}` for .NET Core and
.NET. Confirm that `*_PROFILER_PATH` names a file that exists, that its bitness
matches the process, and that `NEWRELIC_HOME` or `CORECLR_NEWRELIC_HOME` points
at an agent home directory that holds `newrelic.config`.

## Next ask

The Windows Event Viewer entries under Applications and Services Logs, then
Application, at the time the process started. Also the full environment of the
process: `COR_ENABLE_PROFILING`, `COR_PROFILER`, `COR_PROFILER_PATH`,
`NEWRELIC_HOME` for .NET Framework, and the `CORECLR_` equivalents for .NET.
