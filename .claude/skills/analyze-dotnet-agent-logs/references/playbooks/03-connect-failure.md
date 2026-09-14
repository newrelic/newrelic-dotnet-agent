---
id: 3
title: Connect failure
tier: verified
scope: managed
min_level: info
precedence: 30
signatures:
  - "Connection failed: Potential issue with license key based on HTTP status code 401"
  - "Unable to connect to the New Relic service at"
  - 'Received a 401 Unauthorized response invoking method "connect"'
  - "The server has requested that the agent disconnect. The agent is shutting down"
  - "Check your proxy settings"
keywords: [connect, license key, proxy, tls]
verified_versions: "10.54.0"
---

**Symptom.** The agent starts, then no data arrives.

**Read the success pair first.** `Agent {identifier} connected to {host}:{port}`
followed by `Agent fully connected.` means the connect handshake succeeded, so
move to another playbook.

**The fault lines.** Rejected credentials give `Connection failed: Potential
issue with license key based on HTTP status code 401 - Unauthorized.` at WARN,
and the detail line `Received a 401 Unauthorized response invoking method
"connect"` at DEBUG. 401 and 409 trigger a restart, so the session shows
repeating `preconnect` and `connect` attempts. An unreachable collector gives
`Unable to connect to the New Relic service at {host}` at ERROR, with the
exception attached. A server-requested stop gives `The server has requested that the agent
disconnect. The agent is shutting down.` then `Shutting down: {message}` on a
410. A proxy failure carries `Check your proxy settings ({proxy})` in exception
text. `Current TLS Configuration
(System.Net.ServicePointManager.SecurityProtocol): {protocols}` is logged at
INFO on every connect: on .NET Framework a value without TLS 1.2 explains a
handshake failure, so read the value, not the presence of the line.

**Verdict.** Read the status code, then the table in
[collector-protocol.md](../collector-protocol.md). 401 means the license key is
wrong for that host. 407 means the proxy demands authentication. A DNS or socket
error means the collector host is unreachable.

**Limit.** The URI never appears in a DEBUG line, so the log usually does not
show which collector host was tried unless an exception quoted it.

## Customer fix

Confirm the license key belongs to the account and region the agent is pointed
at, because a key from one region is rejected by another. When a proxy is in
use, set `<proxy host=... port=... user=... password=... />` in
`newrelic.config` so the agent authenticates to it. On .NET Framework, enable
TLS 1.2 for the application, either in the framework configuration or by
setting `ServicePointManager.SecurityProtocol` at startup.

## Next ask

`newrelic.config` for the proxy and host settings, and confirmation of which
region the account is in.
