---
name: triage-dotnet-agent-logs
description: Triage a New Relic .NET agent log from a support ticket. Use when a ticket carries a newrelic_agent_*.log or a logs directory, or when the customer reports that the agent sends no data, will not connect, does not instrument a library, or that custom instrumentation XML has no effect.
---

# Triage .NET agent logs

One command reads the log and routes to a playbook. The routing is code, not
judgment: the signatures live in the playbook files this skill ships.

## The script

Every command below runs this one file. Set it once per session:

```
NRLOG="${CLAUDE_PLUGIN_ROOT}/skills/triage-dotnet-agent-logs/scripts/nrlog.py"
```

`${CLAUDE_PLUGIN_ROOT}` is the plugin's install directory. It changes on every
plugin update, so never hard-code the path you saw last session. The playbooks
are beside it, under
`${CLAUDE_PLUGIN_ROOT}/skills/triage-dotnet-agent-logs/references/`.

## Hard rules

- **`nrlog.py` is the only tool that opens the log.** Never `Read`, `cat`,
  `tail`, or grep a customer log. A dump runs to hundreds of MB with single
  lines tens of KB wide, and those bytes stay in context for the whole session.
- **Report `BLOCKED` as blocked.** A playbook the log's level cannot evaluate is
  not a playbook that did not match. Saying "clear" there sends the engineer
  down the wrong path.
- **Never change a `## Customer fix`.** Adapt tone and substitute the customer's
  own path or process name. The technical content is reviewed data.
- **The escalation packet is internal.** It carries host names, application
  names, SQL text, and request parameters. It never goes to a customer.
- **Name the limit in every verdict.** "Consistent with X, not proven, because
  that path logs nothing at this level" is a finished answer.

## Workflow

0. **Preflight.** Run `python3 --version`. On failure try `python --version`,
   then `py -3 --version`. If none answers, tell the engineer to install
   Python 3 from python.org and stop. Use the launcher that answered as `$PY`
   for every command below, and run them as `$PY "$NRLOG" <command>`.
1. **Triage.** `nrlog.py triage <path>`. Pass the ticket's log file or its whole
   logs directory. If it reports more than one managed log, show the file table
   and ask which application; rerun with `--file <name>`. If it names other
   sessions, ask before rerunning with `--session N`.
2. **Read the matched playbooks only.** The `NEXT` line of the report names
   them. Read those files and nothing else under `references/playbooks/`.
3. **Answer.** Verdict in chat, evidence lines quoted verbatim from the report,
   the limit stated. A `[field]` tier means the signature was confirmed by
   observation, not by reading agent source; say so.
4. **Customer block.** `nrlog.py summary <path> --playbook N --format customer`.
   For a field playbook the block carries an unverified header addressed to you,
   not to the customer. Decide whether to send it, then remove that header.
5. **Escalate when unmatched or unresolved.** `nrlog.py summary <path>
   --escalation --ticket <id>` builds the packet. Attach it to the internal
   escalation instead of the raw dump.
6. **Capture, when the ticket resolves unmatched.** `nrlog.py draft-playbook
   <path>` writes a filled skeleton. Fill the two headings and open a pull
   request adding it under `teams/dotnet-agent/playbooks/field/` in the
   marketplace repository. A ticket
   closes at the moment of highest knowledge, and that is the moment to write it
   down.

## Commands

| Command | Use it to |
|---|---|
| `triage` | Everything above: session pick, profiler groups, matched and blocked playbooks, version currency |
| `summary` | Produce the customer block, or build the escalation packet |
| `slim` | Write a redacted, payload-stripped, level-narrowed file when a verdict needs one more look |
| `profiler` | Group a directory of `NewRelic.Profiler.<pid>.log` files by signature |
| `instrumented` | List the methods the profiler rewrote, for a custom-instrumentation question |
| `draft-playbook` | Emit a field-playbook skeleton from the current triage state |

Run any command with `--help` for its flags. Derived files land in
`nrlog-work/` beside the source log. The source log is never modified. Nothing
is written inside the plugin directory, which a plugin update replaces.

## When nothing matches

State it plainly, give the next ask from the closest `BLOCKED` playbook, and
build the escalation packet. Not knowing is a finished answer with an action
attached. Then run `draft-playbook` when the ticket resolves.

## References

- `references/log-basics.md` - line layouts, level names, session identity.
  Read only when a question outruns the report.
- `references/playbooks/` - one file per symptom. Read the matched ones.
