---
id: 2
title: .NET Framework allow-list rejection
tier: verified
scope: profiler
min_level: info
precedence: 20
signatures:
  - "is not configured to be instrumented"
  - "should not be instrumented, unloading profiler"
keywords: [allow-list, included application, process name]
verified_versions: "10.54.0"
---

**Symptom.** A profiler log exists, no managed agent log exists, and the app is
.NET Framework and not hosted in IIS. In a whole-directory dump this is the
largest group by far and it is expected noise: one rejected process per
short-lived executable on the host.

**Verdict.** Proven. On .NET Framework the profiler instruments only an
allow-listed set of process names: `w3wp.exe` and its children,
`WebDev.WebServer40.exe`, `WebDev.WebServer20.exe`, `inetinfo.exe`,
`WaWorkerHost.exe`, `WaWebHost.exe`, `WcfSvcHost.exe`. Everything else unloads
the profiler before the managed agent starts, so no managed log is ever created.

**Limit.** .NET Framework only. .NET Core and .NET have no allow-list, so this
playbook never applies there.

## Customer fix

Add the process to the instrumentation allow-list, with either the environment
variable `NEW_RELIC_INCLUDED_APPLICATION_NAMES=MyApp.exe`, or an
`<application name="MyApp.exe" />` entry under
`<instrumentation><applications>` in `newrelic.config`. The environment
variable wins: when it is set, the config list is not read. Matching is a
suffix match on the full process path, so a bare executable name works.

## Next ask

None. The log settles it.
