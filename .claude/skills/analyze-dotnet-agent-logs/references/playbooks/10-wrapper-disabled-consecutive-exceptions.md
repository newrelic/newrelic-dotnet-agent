---
id: 10
title: Wrapper hard-disabled after too many consecutive exceptions
tier: verified
scope: managed
min_level: error
precedence: 45
signatures:
  - "due to too many consecutive exceptions"
  - "This will reduce the functionality of the agent until the agent is restarted"
  - "MsSqlConnectionStringParser.ParsePortPathOrId"
keywords: [wrapper, datastore, connection string, consecutive exceptions, missing data, sql]
verified_versions: "10.54.0"
---

**Symptom.** Data of one kind stops appearing partway through the process's life. This
often starts minutes after the process starts, and is often first reported right after
an agent upgrade. The application runs fine. The agent has switched off one wrapper.

**Mechanism.** A tracer inside one wrapper throws exceptions on consecutive calls. When
the count of consecutive exceptions crosses an agent-health threshold, the agent
disables that wrapper for that one method, for the rest of the process's life. The agent
logs an ERROR line when this happens. Other methods that use the same wrapper keep
working. This is why the symptom is often partial, not total.

**Restart is not a fix.** A restart or an app-pool recycle clears the disabled state,
but only until the same input triggers the same exceptions again. Treat a restart as a
temporary workaround, not a resolution.

**Leading known cause.** `MsSqlConnectionStringParser.ParsePortPathOrId()` throws
`ArgumentOutOfRangeException` on some connection strings. The agent logs this as
`Tracer invocation error`. Each of `ExecuteReader`, `ExecuteScalar`, and
`ExecuteNonQuery` can trip the threshold on its own, so any one, two, or all three
methods can go dark independently. This is issue #3263, fixed in agent version 10.45.0.

**Limit.** The disable line names the wrapper and the method, not the root cause. When
the known cause above does not match, treat the wrapper and method names as the lead,
and look for the exception type in the ERROR block above the disable line.

## Customer fix

For the MSSQL connection-string cause, upgrade to agent 10.45.0 or later. For any other
wrapper, read the wrapper name and method name from the disable line, and use them to
find the code path that the wrapper covers. That code path is where the underlying
exception starts.

## Next ask

The full ERROR block around the first disable line, including the exception and its
stack trace, and the agent version.
