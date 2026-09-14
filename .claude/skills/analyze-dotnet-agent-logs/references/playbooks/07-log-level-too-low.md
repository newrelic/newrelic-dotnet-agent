---
id: 7
title: Log level too low to diagnose
tier: verified
scope: managed
min_level: info
precedence: 5
signatures:
level_max: info
keywords: [log level, verbosity, observed counts]
verified_versions: "10.54.0"
---

**Symptom.** The log looks healthy and answers nothing.

**How this playbook matches.** It carries no signature. It matches on the
session's observed level: when the most verbose level actually present is INFO or
quieter, the log cannot answer a payload, environment, or wrapper question.

**Supporting lines.** `Log level set to {level}` states the level at startup.
`Invalid log level '{value}' specified. Using log level 'Info' by default.`, `The
log level, {value}, set in your configuration file has been deprecated...`, and
`Log level was set to "Audit" which is not a valid log level. ... Log level will
be treated as INFO for this run.` each explain why the level is not what the
customer expected.

**Verdict.** At INFO there are no collector payloads, no environment variables,
and no wrapper decisions. Report the level and stop rather than reading absence
as evidence.

Read the level from the observed token counts, not from `Log level set to`. That
banner is written once at startup and goes stale as soon as the level changes
(see playbook 9).

**Limit.** This is the playbook that tells you the others cannot run yet.

## Customer fix

Set `NEW_RELIC_LOG_LEVEL=debug` in the environment of the process, or
`<log level="debug" />` in `newrelic.config`, then restart the application and
let it run for at least two minutes so two harvest cycles are captured. Use
`finest` only when asked for it, because a finest log grows very large.

## Next ask

`NEW_RELIC_LOG_LEVEL=debug` and a fresh log covering a restart plus at least two
harvest cycles, which is about two minutes at the default interval. Ask for
`finest` only when playbook 8 is in play, because it is very large.
