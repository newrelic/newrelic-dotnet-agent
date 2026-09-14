---
id: 9
title: Runaway log volume
tier: verified
scope: managed
min_level: info
precedence: 70
signatures:
stated_differs_from_observed: true
keywords: [log volume, level change, rolling files]
verified_versions: "10.54.0"
---

**Symptom.** The agent log fills the disk, or a support dump arrives as several
files sitting exactly at the rolling limit.

**How this playbook matches.** It carries no signature. It matches when the
stated level disagrees with the observed level: the startup banner says one level
and the file holds a more verbose one.

**The line.** `The log level was updated to FINEST from INFO`, at INFO, once per
app domain in the process. It is not a signature. The agent also writes it during
startup when the level is set in `newrelic.config` rather than by environment
variable, because the first configuration update compares the customer's level
against the built-in default of `info`. That startup line is benign, and it sits
before the `Log level set to` banner rather than after it. Stated against
observed separates the two cases without reading positions.

**Verdict.** The level was raised at runtime, so the startup banner still says
INFO while the file fills with FINEST. One `newrelic.config` edit produces one of
these lines per app domain in the process, within seconds of each other. Give the
engineer the timestamp of the change and the line count on each side of it: that
pair is the whole diagnosis.

**Limit.** The line records that the configuration changed, not who changed it.

## Customer fix

Set the log level back to `info` in `newrelic.config`, or with
`NEW_RELIC_LOG_LEVEL=info`, so the file stops growing. The agent picks up the
`newrelic.config` change without a restart. Cap the damage with
`<log maxLogFileSizeMB="100" maxLogFiles="4" />`, which bounds the disk the logs
can take but does not stop the volume itself.

## Next ask

Whether anyone raised the log level for troubleshooting and forgot to lower it,
and the current `<log level="..." />` value in `newrelic.config`. Rolling limits
(`maxLogFileSizeMB`, `maxLogFiles`) bound the damage but do not stop it.
