# Log formats and session identity

Every line layout, level name, and file-naming rule below is taken from agent
source. Sample lines are synthetic, built to match the verified format.

## The files

| File | Written by | Holds |
|---|---|---|
| `newrelic_agent_<name>.log` | managed agent | config, connect, harvests, wrapper activity |
| `newrelic_agent_<name>_NNN.log` | managed agent | an older slice of the same log, after size rolling |
| `NewRelic.Profiler.<pid>.log` | native profiler | process decision, instrumentation XML, rewritten methods |
| `newrelic_audit.log` | managed agent | collector traffic, only when `auditLog` is enabled |

`<name>` resolves in this order: `NEW_RELIC_LOG`, then the `fileName` in
`newrelic.config`, then on .NET Framework the IIS `AppDomainAppId`, then the
app-domain name or process name. So one IIS site gets one file, and a console
host gets `newrelic_agent_MyApp.log`.

Managed log directory: `NEW_RELIC_LOG_DIRECTORY` or `NEWRELIC_LOG_DIRECTORY`,
else the `logs` directory under the agent home.

Profiler log directory, in precedence order:
`NEW_RELIC_PROFILER_LOG_DIRECTORY`, `NEW_RELIC_LOG_DIRECTORY`, an Azure App
Service special case, `<NEW_RELIC_HOME>/Logs` (`logs` on Linux), then the
platform default under common application data.

The audit log carries nothing a DEBUG-level managed log lacks, so it is out of
scope. Recognize it by its layout and move on:
`2026-08-18 14:03:11,123 NewRelic Audit: Data Sent from the Collector : ...`

## Managed log line

Layout (`LoggerBootstrapper.cs`):

```
{UTCTimestamp} NewRelic {NRLogLevel,6}: [pid: {pid}, tid: {tid}] {Message}
{Exception}
```

Sample:

```
2026-08-18 14:03:11,123 NewRelic   INFO: [pid: 8412, tid: 1] The New Relic .NET Agent v10.44.0 started (pid 8412) on app domain '/LM/W3SVC/2/ROOT'
```

- Timestamp is `yyy-MM-dd HH:mm:ss,fff` from `DateTimeOffset.UtcNow`. Always
  UTC. Comma before the milliseconds.
- The level token is right-aligned in six characters, so `  INFO`, ` DEBUG`,
  `FINEST`, `  WARN`, ` ERROR`, ` Audit`. Match it with `\s*` on the left.
- Anchor regex:
  `^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3}) NewRelic\s+(\w+): \[pid: (\d+), tid: (\d+)\] (.*)$`
- A line that fails the anchor is a **continuation**: an exception message or
  stack frame appended to the entry above it. Attach it to the preceding entry
  of the same stream.

### Levels

In-file tokens are `FINEST`, `DEBUG`, `INFO`, `WARN`, `ERROR`, `Audit`.

`Log level set to {level}` is written once, at startup. The level can change
later: `ConfigurationService` compares the old and new value, logs `The log
level was updated to {new} from {previous}` at INFO, and swaps the Serilog
level switch. So the startup line goes stale, and a session can state INFO
while holding hundreds of thousands of FINEST lines. Count the level tokens
rather than believing the banner - `nrlog.py sessions` and `extract` both
report stated and observed side by side.

Configured values map to those, including deprecated aliases
(`LogLevelExtensions.cs`): `VERBOSE`, `FINE`, `FINER`, `FINEST`, `TRACE`, `ALL`
all mean FINEST. `NOTICE` means INFO. `ALERT` means WARN. `CRITICAL`,
`EMERGENCY`, `FATAL`, `SEVERE` all mean ERROR. `OFF` disables the log. An
unrecognized value falls back to INFO with a warning line.

What each level unlocks:

| Level | Adds |
|---|---|
| INFO | startup banner, app names, connect success, high security, app-name-in-use, warnings |
| DEBUG | environment variables, full collector request and response bodies, response headers, runtime version |
| FINEST | per-request `Invoking`, skipped wrappers, segment detail |

DEBUG is the level most support questions need. FINEST is the only level that
shows a wrapper skipped for lack of a transaction.

## Profiler log line

Layout (`Logger.h`):

```
[{Level}] {timestamp} {message}
```

Sample:

```
[Info ] 2026-08-18 14:03:09 Profiler initialized
```

- Level strings are `Trace`, `Debug`, `Info `, `Warn `, `Error`. `Info ` and
  `Warn ` carry a trailing space inside the brackets.
- Timestamp is `%Y-%m-%d %X` over `gmtime_s`: UTC, whole seconds, no
  milliseconds and no zone marker.
- No pid and no tid on the line. The pid is in the file name only.
- Anchor regex: `^\[(\w+)\s*\] (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) (.*)$`
- Level comes from the same `NEW_RELIC_LOG_LEVEL` value as the managed agent.
  It is clamped to INFO whenever console logging is on, or in Azure Functions
  mode without `NEW_RELIC_AZURE_FUNCTION_LOG_LEVEL_OVERRIDE`. A customer can
  therefore set `finest` and still hand you an INFO-only profiler log.

## Rolling and concurrent writers

The Serilog file sink is configured with `shared: true`
(`LoggerBootstrapper.cs:280`). Two consequences:

- **Interleaving.** Concurrent processes writing the same file name - an IIS web
  garden, an overlapped recycle - interleave their lines. Lines are not in
  per-run order, so read each pid as its own stream.
- **Rolling.** Size rolling (`maxLogFileSizeMB`, `maxLogFiles`) appends a
  numeric suffix; daily rolling appends a date. Order rolled siblings by their
  first parsed timestamp rather than by file name, so neither naming pattern
  matters.

## Session identity

A **session** is one agent run: a pid plus a contiguous UTC range.

Anchors:

- Start: `The New Relic .NET Agent v{version} started (pid {pid}) on app domain '{domain}'` (INFO)
- End: `The New Relic .NET Agent v{version} has shutdown (pid {pid}) on app domain '{domain}'` (INFO)

Rules:

- Count banners; do not split on the first one. On .NET Framework one
  `w3wp.exe` hosts several IIS applications at once, and every app domain
  writes its own `started` banner into the same file. Nine app domains in one
  pid is ordinary. Treat the pid as one session that hosts N app domains, and
  split only when the shutdown banners balance the start banners, which is the
  point where nothing is left running.
- Merge across rolled files when the pid matches, no `started` banner
  intervenes, and the gap between the last and first timestamp is under five
  minutes. Report the gap when it is larger, and do not merge.
- The app domain appears **only** in the banner. Nothing on an ordinary line
  identifies it.

Flags, because the file often cannot prove what you want:

| Flag | Means |
|---|---|
| `head-truncated` | no `started` banner; agent version and app domain unknown |
| `tail-truncated` | no `shutdown` banner; indistinguishable from still running |
| `appdomain-unknown` | no banner in range, so only the pid is known |
| `appdomain-ambiguous` | this pid hosted several app domains, so no line can be attributed to one |
| `interleaved` | another pid wrote into the same range |

Each app domain in a shared pid has its own configuration, so they can run at
different log levels and change level independently. A single `newrelic.config`
edit shows up as one `The log level was updated to ...` line per app domain,
within a few seconds of each other.

Per-line app-domain attribution is impossible in a shared pid: the app domain
appears only in the banner, never on an ordinary line. Report
`appdomain-ambiguous` and name the app domains rather than choosing one.

## Redaction

Derived files are written with these removed: license key, security policies
token, proxy user and password, config obscuring key, `Authorization` header
values.

Host names, application names, SQL text, and request parameters pass through
unchanged. They are evidence, and removing them would break the playbooks.

The license key is normally absent from a managed log. It travels in the request
URI (`HttpRequest.cs`), which no DEBUG line prints, and the reported settings
expose only `agent.license_key.configured` as a boolean because
`ReportedConfiguration.AgentLicenseKey` is `[JsonIgnore]`. The realistic leak
path is exception text that quotes the failing URI, which is why the redaction
runs anyway.
