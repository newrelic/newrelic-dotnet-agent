---
id: 4
title: Connected but no data of one type
tier: verified
scope: profiler
min_level: info
precedence: 50
signatures:
  - "for rejit. HR:"
  - "An exception was thrown while reading instrumentation file"
  - "Unable to parse one or more instrumentation files."
  - "Unable to find the New Relic Agent extensions directory"
keywords: [instrumentation xml, rejit, extensions, data type]
verified_versions: "10.54.0"
---

**Symptom.** The agent connects, some data appears in the UI, one kind does not.

Work the three branches in order.

**Branch A: the server turned it off.** In the `connect` response, look for
`collect_span_events`, `collect_analytics_events`, `collect_error_events`,
`collect_custom_events`, `collect_traces`, `collect_errors`, `collect_ai` set to
`false`. A `false` here fully explains the absence, and no agent-side change will
fix it.

**Branch B: nothing was ever sent.** In `nrlog.py payloads`, check whether the
endpoint for that data type appears at all. When `span_event_data` never appears,
the aggregator was empty every harvest, which means the instrumentation never
produced the data. Then check the profiler side:

- `nrlog.py instrumented <profiler log>` and look for the customer's type or
  method. `Instrumenting method: {signature}` at INFO is proof the profiler
  rewrote it.
- `Unable to find {ClassName} for rejit. HR:{hr}` at INFO means the
  instrumentation XML names a class the profiler cannot find. Paired with a
  missing `Instrumenting method:` line, this is the signature of custom
  instrumentation XML that names a class or method wrongly.
- `An exception was thrown while reading instrumentation file: {path} - ignoring
  this file.` at ERROR means that XML file was skipped entirely.
- `Unable to parse one or more instrumentation files.` means the same for the
  live-reload path.
- `Unable to find the New Relic Agent extensions directory ({dir}).` at WARN
  means almost nothing will be instrumented.

**Branch C: it was sent and rejected.** The endpoint appears, and the response is
a 4xx. Read the status table in [collector-protocol.md](../collector-protocol.md).

**Limit.** Branch B cannot distinguish "wrapper matched but produced nothing"
from "wrapper never ran" without FINEST. Playbook 8 covers the common reason.

## Customer fix

Correct the custom instrumentation XML so every `className` and `methodName`
matches the assembly exactly, including the namespace and the parameter types,
then confirm the file is valid XML and sits in `<agent-home>/extensions/`. When
the extensions directory itself is missing, reinstall the agent so the directory
is restored. When the data type is switched off on the server side, change that
setting in the New Relic UI for the application; no local change has any effect.

## Next ask

The customer's custom instrumentation XML file and the contents of
`<agent-home>/extensions/`, plus a FINEST-level log if branches A and C are
ruled out.
