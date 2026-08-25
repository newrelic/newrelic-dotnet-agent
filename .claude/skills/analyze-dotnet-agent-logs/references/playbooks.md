# Playbooks

Nine symptoms. Every signature is a string taken from agent source, quoted as
the agent writes it. Each playbook ends with a **next ask**: what to request from
the customer when the log cannot settle the question.

Read [log-formats.md](log-formats.md) for the level each signature needs. Start
with playbook 7 when the level is unknown, because it bounds all the others.

## 1. Profiler never loaded

**Symptom.** No data at all, and no managed agent log.

**Check.** Run `nrlog.py profiler` on the log directory. Above 8 files it groups
them by signature, so a dump of thousands collapses to a handful of groups.

**Signatures.**

- No profiler log file at all: the CLR never loaded the profiler DLL.
- `Profiler initialized` present: the profiler loaded. Move to another playbook.
- `Error initializing CLR profiler info: {hr}`
- `.NET Core 3.1 or greater required. Profiler not attaching.`
- `The global newrelic.config file was not found at: {path}`

**Verdict.** Absent log means the CLR never called the profiler. The usual causes
are a missing or mistyped `*_PROFILER_PATH`, a GUID mismatch, a bitness
mismatch, or missing native dependencies. None of those can write a log, which is
why the absence is the finding.

**Limit.** The absence proves only that the profiler did not run. It does not
say which of those causes applies.

**Next ask.** The Windows Event Viewer entries under Applications and Services
Logs, then Application, at the time the process started. Also the full
environment of the process: `COR_ENABLE_PROFILING`, `COR_PROFILER`,
`COR_PROFILER_PATH`, `NEWRELIC_HOME` for .NET Framework, and the `CORECLR_`
equivalents for .NET.

## 2. .NET Framework allow-list rejection

**Symptom.** A profiler log exists, no managed agent log exists, and the app is
.NET Framework and not hosted in IIS. In a whole-directory dump this is the
largest group by far and it is expected noise: one rejected process per
short-lived executable on the host.

**Signatures.**

```
[Info ] ... This process (C:\Apps\MyApp\MyApp.exe) is not configured to be instrumented.
[Info ] ... This process should not be instrumented, unloading profiler.
```

**Verdict.** Proven. On .NET Framework the profiler instruments only an
allow-listed set of process names: `w3wp.exe` and its children,
`WebDev.WebServer40.exe`, `WebDev.WebServer20.exe`, `inetinfo.exe`,
`WaWorkerHost.exe`, `WaWebHost.exe`, `WcfSvcHost.exe`. Everything else unloads
the profiler before the managed agent starts, so no managed log is ever created.

**Fix to give the customer.** Either
`NEW_RELIC_INCLUDED_APPLICATION_NAMES=MyApp.exe`, or an `<application
name="MyApp.exe" />` entry under `<instrumentation><applications>` in
`newrelic.config`. The environment variable wins: when it is set, the config
list is not read. Matching is a suffix match on the full process path, so a bare
executable name works.

**Limit.** .NET Framework only. .NET Core and .NET have no allow-list, so this
playbook never applies there.

**Next ask.** None. The log settles it.

## 3. Connect failure

**Symptom.** The agent starts, then no data arrives.

**Check.** `nrlog.py payloads --session N` and look at what follows
`preconnect`.

**Signatures.**

- Success: `Agent {identifier} connected to {host}:{port}` then `Agent fully
  connected.`
- Rejected credentials: `Received a 401 Unauthorized response invoking method
  "connect"`. 401 and 409 trigger a restart, so the session shows repeating
  `preconnect` and `connect` attempts.
- Server-requested stop: `The server has requested that the agent disconnect.
  The agent is shutting down.` then `Shutting down: {message}` on a 410.
- Proxy: exception text carrying `Check your proxy settings ({proxy})`.
- TLS: `Current TLS Configuration
  (System.Net.ServicePointManager.SecurityProtocol): {protocols}` at INFO,
  logged on every connect. On .NET Framework a value without TLS 1.2 explains a
  handshake failure.

**Verdict.** Read the status code, then the table in
[collector-protocol.md](collector-protocol.md). 401 means the license key is
wrong for that host. 407 means the proxy demands authentication. A DNS or socket
error means the collector host is unreachable.

**Limit.** The URI never appears in a DEBUG line, so the log usually does not
show which collector host was tried unless an exception quoted it.

**Next ask.** `newrelic.config` for the proxy and host settings, and confirmation
of which region the account is in.

## 4. Connected but no data of one type

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
a 4xx. Read the status table in [collector-protocol.md](collector-protocol.md).

**Limit.** Branch B cannot distinguish "wrapper matched but produced nothing"
from "wrapper never ran" without FINEST. Playbook 8 covers the common reason.

**Next ask.** The customer's custom instrumentation XML file and the contents of
`<agent-home>/extensions/`, plus a FINEST-level log if branches A and C are
ruled out.

## 5. Server-side configuration surprise

**Symptom.** The agent behaves in a way the local `newrelic.config` does not
explain.

**Signatures.**

```
The agent is in high security mode.  No request parameters will be collected and sql obfuscation is enabled.
Server-Side Configuration is enabled.
Server-Side Configuration is enabled, but the agent is configured to ignore it.
The following events will be harvested every {ms}ms: {types}
```

Note the double space after `mode.` in the high-security line.

**Verdict.** When server-side configuration is enabled, `agent_config` in the
connect response overrides local settings. High security mode drops request
parameters and forces SQL obfuscation regardless of local config. Server
`messages` are logged at the level the server assigns, so an unexplained INFO or
WARN line with no agent-side origin is likely one of those.

**Limit.** Needs DEBUG to see `agent_config` itself. At INFO you get only the
three announcement lines.

**Next ask.** A DEBUG log, plus what the customer has set in the UI for that
application.

## 6. Unsupported runtime version

**Symptom.** Partial or absent instrumentation on an old runtime.

**Signatures.**

```
Unsupported installed .NET Framework version {version} detected. Please use a version of .NET Framework >= 4.6.2.
.NET version {version} has reached EOL, and support will be removed in the next major release of the .NET Agent. Please use net8 or newer.
```

**Verdict.** The first is a hard floor. The second is a warning, not a failure,
so it explains nothing on its own. Use it as context, not as a cause.

**Limit.** Both are informational. Neither proves the reported symptom.

**Next ask.** None.

## 7. Log level too low to diagnose

**Symptom.** The log looks healthy and answers nothing.

**Signatures.**

```
Log level set to {level}
Invalid log level '{value}' specified. Using log level 'Info' by default.
The log level, {value}, set in your configuration file has been deprecated...
Log level was set to "Audit" which is not a valid log level. ... Log level will be treated as INFO for this run.
```

**Verdict.** At INFO there are no collector payloads, no environment variables,
and no wrapper decisions. Report the level and stop rather than reading absence
as evidence.

Read the level from the observed token counts, not from `Log level set to`. That
banner is written once at startup and goes stale as soon as the level changes
(see playbook 9).

**Limit.** This is the playbook that tells you the others cannot run yet.

**Next ask.** `NEW_RELIC_LOG_LEVEL=debug` and a fresh log covering a restart plus
at least two harvest cycles, which is about two minutes at the default interval.
Ask for `finest` only when playbook 8 is in play, because it is very large.

## 8. Wrapper skipped for lack of an active transaction

**Symptom.** Instrumentation looks correct, the profiler rewrote the method, and
no segment or transaction appears for it. Common with custom instrumentation on
background work, message consumers, and timers.

**Signature (FINEST only).**

```
No transaction, skipping method MyNamespace.MyClass.MyMethod(System.String)
```

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

**Next ask.** A FINEST-level log covering one execution of the code path, and a
description of what invokes the method - a request, a timer, a queue consumer, or
application startup.

## 9. Runaway log volume

**Symptom.** The agent log fills the disk, or a support dump arrives as several
files sitting exactly at the rolling limit.

**Check.** `nrlog.py sessions`. Compare `stated=` against `observed=`, and read
the `level change:` lines.

**Signature.**

```
The log level was updated to FINEST from INFO
```

**Verdict.** The level was raised at runtime, so the startup banner still says
INFO while the file fills with FINEST. One `newrelic.config` edit produces one of
these lines per app domain in the process, within seconds of each other. Give the
engineer the timestamp of the change and the line count on each side of it: that
pair is the whole diagnosis.

**Limit.** The line records that the configuration changed, not who changed it.

**Next ask.** Whether anyone raised the log level for troubleshooting and forgot
to lower it, and the current `<log level="..." />` value in `newrelic.config`.
Rolling limits (`maxLogFileSizeMB`, `maxLogFiles`) bound the damage but do not
stop it.
