---
name: analyze-dotnet-agent-logs
description: Parse and diagnose New Relic .NET agent logs (newrelic_agent_*.log) and profiler logs (NewRelic.Profiler.<pid>.log) from a support ticket. Use when handed a log file or a logs directory, or when asked why the agent is not reporting, not connecting, not instrumenting a library, or why custom instrumentation XML has no effect.
---

# Analyze .NET agent logs

A support log is evidence, not prose. It is large, it holds more than one agent
run, and it is written by a process you cannot interview. Work it in this order.

## Hard rules

- Work only from a **slim** file. `nrlog.py extract` writes one; every later
  step reads that.
- Never `Read`, `tail`, or wide-grep a raw log. A customer log runs to hundreds
  of MB with single lines tens of KB wide, and those bytes stay in context for
  the rest of the session.
- Lead with counts. `sessions`, `payloads`, and `profiler` all answer in
  summary form. Reach for a body only when a count has told you which body.
- Keep analysis local. Log content goes to no artifact, no ticket comment, and
  no web request unless the engineer asks for that in the moment, and then only
  from a slim file.
- Delegate to a subagent when the source file exceeds 50 MB, or when the answer
  needs a sweep across many sessions or many files. Give the subagent the exact
  question and the exact commands, and require a verdict back, not log content.

## Workflow

1. **Inventory.** Run `nrlog.py sessions <path>` on the file or directory. It
   accepts a rolled set and merges a run that spans siblings. A customer dump
   holds dozens of applications, so above 40 sessions it prints a per-file
   summary instead: pick a file with `--file <name>`, then add `--all`.
2. **Pick the session.** Show the rows. When more than one session is in scope,
   ask the engineer which one before going further. A support question is
   almost always about one run, and the wrong run wastes the whole analysis.
   Session numbers are relative to the `--file` filter, so pass the same
   `--file` to every later command.
3. **Slim it.** Run `nrlog.py extract <path> --session N`. Read the slim file
   from here on. When it reports more than a few MB, narrow with `--level`,
   `--since`, `--until`, or `--grep` and extract again. A 7-hour FINEST session
   slims to 125 MB, which is no more readable than the original.
4. **Read the level from the counts, not the banner.** `extract` prints both
   `stated log level` and `observed levels`. They disagree whenever the level
   changed at runtime, which the agent records as `The log level was updated to
   {new} from {previous}`. Trust the observed counts. The level bounds every
   conclusion: INFO hides all collector payloads, and FINEST is the only level
   that shows a skipped wrapper.
5. **Correlate the profiler.** Point `nrlog.py profiler` at the directory
   first. A dump routinely holds thousands of profiler logs, so above 8 files
   it groups them by signature: most are processes the .NET Framework
   allow-list rejected, which is expected noise, and the group carrying
   `initialized` is the short list worth reading. Then match one
   `NewRelic.Profiler.<pid>.log` to the session by pid **and** overlapping UTC
   range. When the ranges do not overlap the pid was reused and the files are
   unrelated, so say that rather than pairing them.
6. **Route.** Take the engineer's symptom to
   [references/playbooks.md](references/playbooks.md). Answer from the slim
   file when no playbook fits.
7. **Report.** Short verdict in chat with the evidence lines quoted verbatim.
   Long form to a markdown file beside the slim file. A ticket-ready block only
   when the engineer asks.

Every verdict names its evidence and its limit. "Consistent with X; not proven,
because that path logs nothing" is a finished answer. A guess dressed as a
finding is not.

## The script

`scripts/nrlog.py`, Python 3 standard library only. Run `--help` for flags.

| Command | Use it to |
|---|---|
| `sessions` | List runs in a file or directory, with truncation and interleaving flags |
| `extract` | Write the slim, redacted, payload-stripped file for one session |
| `payloads` | Index collector calls: time, endpoint, direction, size, status, request guid |
| `body` | Print one request or response body, so a payload enters context on purpose |
| `decode` | Turn a base64 distributed-trace payload into JSON |
| `profiler` | Summarize a profiler log: init, config, extensions, XML failures, method count |
| `instrumented` | List the methods the profiler rewrote, for checking custom instrumentation |

Launcher: `python` on Windows, `python3` elsewhere.

Derived files land in `nrlog-work/` beside the source log unless `--out` says
otherwise. The source log is never modified.

## Changing this skill

Regenerate the fixtures and re-run the checks before trusting an edit to
`nrlog.py`:

```
python tests/make_fixtures.py && python tests/make_merge_fixtures.py
```

They cover a restart, a rolled sibling, a truncated session, interleaved pids,
one pid hosting three app domains, a runtime level change, DEBUG payload lines,
exception continuation lines, and a planted license key for the redaction check.
Generated fixtures are gitignored.

## References

- [references/log-formats.md](references/log-formats.md) - line layouts, level
  names, session identity, what each file is named and where it lives. Read
  before hand-parsing anything the script does not cover.
- [references/collector-protocol.md](references/collector-protocol.md) -
  endpoints, the healthy call sequence, connect request and response fields,
  positional array keys. Read when interpreting a payload or a response.
- [references/playbooks.md](references/playbooks.md) - eight symptoms, each
  with its verified signature, its verdict, and the next ask for the customer.
