# .NET agent log triage

Diagnoses a New Relic .NET agent log from a support ticket in one command.

## Requirements

- Claude Code with the Bash tool.
- Python 3.8 or later on PATH as `python3`, `python`, or `py -3`.
- No New Relic source checkout. No build. No network access at analysis time.

## Install

Two lines in Claude Code:

```
/plugin marketplace add https://source.datanerd.us/agents/claude-skills.git
/plugin install dotnet-log-triage@nr-agents
```

To check your machine first, run the preflight script from the root of a checkout
of this repository. It probes Python, git, and your git credentials, then prints
the lines above. It changes nothing:

```
bash plugins/dotnet-log-triage/bootstrap.sh
```

On Windows PowerShell, run `plugins/dotnet-log-triage/bootstrap.ps1` the same way.
Do not pipe it from the network; PowerShell execution policy makes that fail in
ways that are hard to read.

Read [the repository README](../../README.md) once before you install. It carries
the git credential prerequisite for `source.datanerd.us`, the two settings this
internal marketplace needs to keep working, and the raw fetch URL for the preflight
scripts when you have no checkout. Those apply to every plugin here, not only to
this one.

## Use it

Point it at the ticket's log file or its logs directory:

```
Triage the logs in C:\tickets\12345\logs
```

The skill runs `nrlog.py triage`, reads the playbooks that matched, and answers
with the verdict, the evidence, and the limit. Ask for the customer reply block
or the escalation packet when you want them.

## What it can tell you

Nine verified symptoms, each with a customer fix and a next ask: the profiler
never loaded, the .NET Framework allow list rejected the process, connect
failed, one data type is missing, server-side configuration overrode the local
file, the runtime is unsupported, the log level is too low to diagnose, a
wrapper was skipped for lack of a transaction, and runaway log volume. Field
playbooks contributed by support add to that set and are marked `[field]`.

## What it cannot tell you

Whether the customer's own code is at fault, anything the log level did not
record, and anything about a process that never loaded the profiler and never
wrote a log. The report states its own limits; trust them.

## Contribute a field playbook

Support owns `teams/dotnet-agent/playbooks/field/`. That is the one directory in
this repository a human edits for this plugin, and
[its README](../../teams/dotnet-agent/playbooks/field/README.md) holds the id
ranges, the frontmatter schema, the review norm, and the promotion rule.

A field playbook says the signature was confirmed by observation in a real
customer log, not by reading agent source. `triage` prints `[field]` on every
such match, and the customer block carries an unverified header for you to
remove after you have read it.

## This directory is generated

Every file under `plugins/dotnet-log-triage/`, this README and
`lint_playbooks.py` included, is written by
`tools/support-log-plugin/export.py` in the `newrelic-dotnet-agent` repository and
rebuilt on every release, so an edit here is lost at the next export. Send a change
to `tools/support-log-plugin/` in that repository instead.
