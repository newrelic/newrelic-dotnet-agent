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
scope. Recognize it by its layout and move on.

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

### Levels

In-file tokens are `FINEST`, `DEBUG`, `INFO`, `WARN`, `ERROR`, `Audit`.

`Log level set to {level}` is written once, at startup, but the level can
change later - `ConfigurationService` then logs `The log level was updated to
{new} from {previous}` at INFO. The startup line can go stale; count the
level tokens rather than believing the banner.

Configured values map to those, including deprecated aliases: `VERBOSE`,
`FINE`, `FINER`, `FINEST`, `TRACE`, `ALL` all mean FINEST. `NOTICE` means INFO.
`ALERT` means WARN. `CRITICAL`, `EMERGENCY`, `FATAL`, `SEVERE` all mean ERROR.
`OFF` disables the log. An unrecognized value falls back to INFO with a
warning line.

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
  milliseconds and no zone marker. No pid and no tid on the line.
- Level comes from the same `NEW_RELIC_LOG_LEVEL` value as the managed agent,
  but is clamped to INFO in Azure Functions mode unless
  `NEW_RELIC_AZURE_FUNCTION_LOG_LEVEL_OVERRIDE` is set, so a customer can set
  `finest` and still hand you an INFO-only profiler log.

A run can span rolled sibling files with no boundary marker of its own.

## Session identity

A session is one agent run: a pid plus a contiguous UTC range.

Anchors:

- Start: `The New Relic .NET Agent v{version} started (pid {pid}) on app domain '{domain}'` (INFO)
- End: `The New Relic .NET Agent v{version} has shutdown (pid {pid}) on app domain '{domain}'` (INFO)

Rules:

- Count banners; do not split on the first one. On .NET Framework one
  `w3wp.exe` hosts several IIS applications at once, and every app domain
  writes its own `started` banner into the same file. Treat the pid as one
  session that hosts N app domains, and split only when the shutdown banners
  balance the start banners.
- Merge across rolled files when the pid matches, no `started` banner
  intervenes, and the gap between the last and first timestamp is under five
  minutes. Report the gap when it is larger, and do not merge.
- A pid match without a UTC overlap is a different process, not the same
  session picked back up.

Flags, because the file often cannot prove what you want:

| Flag | Means |
|---|---|
| `head-truncated` | no `started` banner; agent version and app domain unknown |
| `tail-truncated` | no `shutdown` banner; indistinguishable from still running |
| `appdomain-unknown` | no banner in range, so only the pid is known |
| `appdomain-ambiguous` | this pid hosted several app domains, so no line can be attributed to one |
| `interleaved` | another pid wrote into the same range |
