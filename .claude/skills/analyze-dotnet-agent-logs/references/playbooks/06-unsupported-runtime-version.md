---
id: 6
title: Unsupported runtime version
tier: verified
scope: managed
min_level: warn
precedence: 15
signatures:
  - "Unsupported installed .NET Framework version"
  - "has reached EOL, and support will be removed in the next major release"
keywords: [runtime version, framework, eol]
verified_versions: "10.54.0"
---

**Symptom.** Partial or absent instrumentation on an old runtime.

**The two lines.** `Unsupported installed .NET Framework version {version}
detected. Please use a version of .NET Framework >= 4.6.2.` and `.NET version
{version} has reached EOL, and support will be removed in the next major release
of the .NET Agent. Please use net8 or newer.` The agent writes both at WARN.

**Verdict.** The first is a hard floor. The second is a warning, not a failure,
so it explains nothing on its own. Use it as context, not as a cause.

**Limit.** Both are informational. Neither proves the reported symptom.

## Customer fix

Move the application to a supported runtime. .NET Framework 4.6.2 is the floor
for the .NET Framework agent. For .NET Core and .NET, use net8 or newer, because
support for a runtime that has reached end of life is removed in the next major
agent release.

## Next ask

None.
