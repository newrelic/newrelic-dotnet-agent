---
id: 5
title: Server-side configuration surprise
tier: verified
scope: managed
min_level: info
precedence: 40
signatures:
  - "The agent is in high security mode"
  - "Server-Side Configuration is enabled"
keywords: [server-side configuration, high security, harvest cycle]
verified_versions: "10.54.0"
---

**Symptom.** The agent behaves in a way the local `newrelic.config` does not
explain.

**The announcement lines.** The agent writes
`The agent is in high security mode.  No request parameters will be collected
and sql obfuscation is enabled.` with a double space after `mode.`, then either
`Server-Side Configuration is enabled.` or `Server-Side Configuration is
enabled, but the agent is configured to ignore it.`, then `The following events
will be harvested every {ms}ms: {types}`. The harvest line is not a signature:
the agent writes it on any connect whose response enables faster event harvest,
so it proves nothing by itself. Read it for the interval, not for its presence.

**Verdict.** When server-side configuration is enabled, `agent_config` in the
connect response overrides local settings. High security mode drops request
parameters and forces SQL obfuscation regardless of local config. Server
`messages` are logged at the level the server assigns, so an unexplained INFO or
WARN line with no agent-side origin is likely one of those.

**Limit.** Needs DEBUG to see `agent_config` itself. At INFO you get only the
three announcement lines.

## Customer fix

Change the setting where it is actually held. Open the application settings in
the New Relic UI and turn off high security mode, or turn off server-side
configuration, so the local `newrelic.config` takes effect again. To keep the
server settings and have this agent ignore them, set
`NEW_RELIC_IGNORE_SERVER_SIDE_CONFIG=true` in the environment of the process.
The agent then logs `Server-Side Configuration is enabled, but the agent is
configured to ignore it.`

## Next ask

A DEBUG log, plus what the customer has set in the UI for that application.
