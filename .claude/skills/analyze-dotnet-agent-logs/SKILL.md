---
name: analyze-dotnet-agent-logs
description: Parse and diagnose New Relic .NET agent logs (newrelic_agent_*.log) and profiler logs (NewRelic.Profiler.<pid>.log) from a support ticket. Use when handed a log file or a logs directory, or when asked why the agent is not reporting, not connecting, not instrumenting a library, or why custom instrumentation XML has no effect.
---

# Analyze .NET agent logs

A support log is evidence, not prose. It is large, it holds more than one agent
run, and it is written by a process you cannot interview. Work it in this order.

## Hard rules

- `nrlog.py` is the only tool that opens the log. Run its commands (`triage`,
  `summary`, `slim`) against the raw path; a slim file is for when a verdict
  needs another look.
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
- Ask the engineer for the reported symptom before you conclude, if they have
  not stated one. A match cannot be tested against a symptom that is not on
  the table.
- A mechanism is not a cause. When the customer reports the behaviour changed
  after an upgrade, a mechanism found in the log - no transaction, a disabled
  wrapper, a missing segment - is the how, not the why. Something in the
  version delta produced it; report the mechanism and that delta together,
  never the mechanism alone.
- Use the version window. When the report names a version where it worked,
  pass `--worked-on <that version>` to `triage` and read every entry between
  the two versions, not only what a keyword happens to match. When the report
  does not name one, ask for it; it is usually the cheapest question on the
  ticket.

## Workflow

1. **Preflight.** Run `nrlog.py sessions <path>` on the file or directory. It
   accepts a rolled set and merges a run that spans siblings. A customer dump
   holds dozens of applications, so above 40 sessions it prints a per-file
   summary instead: pick a file with `--file <name>`, then add `--all`. When
   more than one application is in scope, ask the engineer which one before
   going further. Session numbers are relative to the `--file` filter, so pass
   the same `--file` to every later command.
2. **Triage.** Run `nrlog.py triage <path> --file <name>`. This is the entry
   point and it answers most tickets on its own. One report gives the chosen
   session, the observed level that bounds every verdict, the correlated
   profiler logs, the playbooks that matched with their evidence lines, the
   playbooks blocked by too low a level, and the agent version against the
   changelog. It ends with a NEXT line naming the playbook files to read and the
   `slim` command for this session.
3. **Read the matched playbook, then test it against the symptom.** Open the
   files the NEXT line names. Each one carries the verified signature, the
   verdict, the customer fix, and the next ask. A `[field]` label means
   field-observed, not source-verified: read it before you act on it. For each
   matched playbook, decide whether its mechanism produces the symptom the
   engineer reported: yes, no, or cannot tell from this log. **Hard rule: if no
   matched playbook explains the reported symptom, report the ticket as
   unmatched**, and go to escalation and `draft-playbook`. Never lead with the
   best available match when it does not explain the complaint; state it as
   "present in the log, does not explain your symptom", not as the verdict.
   When nothing matched, the report says so; answer from the slim file, and
   consider `draft-playbook` to start a new one.
4. **Answer the customer.** Run `nrlog.py summary <path> --playbook N` for the
   reply block: the fix and the next ask, with no log line in it. A field-tier
   block carries a header addressed to you and not to the customer.
5. **Escalate.** Run `nrlog.py summary <path> --file <name> --escalation
   --ticket <id>` when the ticket goes to engineering. Carry the same
   `--session N` that `triage` reported, the same way you carry `--file`.
   It writes a directory holding the triage report, a level-narrowed redacted slim log
   (`ERROR`, `WARN`, `INFO`), a redacted copy of each correlated profiler log,
   and `environment.txt`. The packet holds host and application names, so it
   goes to the internal escalation and never into a customer-facing reply. Add
   `--zip` for one file to attach.

Read the level from the counts, not the banner. `triage` and `slim` both print
`stated` and `observed` levels. They disagree whenever the level changed at
runtime, which the agent records as `The log level was updated to {new} from
{previous}`. Trust the observed counts: INFO hides all collector payloads, and
FINEST is the only level that shows a skipped wrapper.

Every verdict names its evidence and its limit. "Consistent with X; not proven,
because that path logs nothing" is a finished answer. A guess dressed as a
finding is not.

## The script

`scripts/nrlog.py`, Python 3 standard library only. Run `--help` for flags.

| Command | Use it to |
|---|---|
| `sessions` | List runs in a file or directory, with truncation and interleaving flags |
| `triage` | **Entry point.** One routed verdict: session, level, profiler, playbook match, version |
| `slim` | Write the slim, redacted, payload-stripped file for one session |
| `summary` | Write the customer reply block, or build the escalation packet |
| `draft-playbook` | Start a field playbook from the shapes no playbook claimed |
| `payloads` | Index collector calls: time, endpoint, direction, size, status, request guid |
| `body` | Print one request or response body, so a payload enters context on purpose |
| `decode` | Turn a base64 distributed-trace payload into JSON |
| `profiler` | Summarize a profiler log: init, config, extensions, XML failures, method count |
| `instrumented` | List the methods the profiler rewrote, for checking custom instrumentation |

`extract` is a hidden alias of `slim`, kept so older notes and saved commands
keep working; write `slim` in anything new.

Launcher: `python` on Windows, `python3` elsewhere.

Derived files land in `nrlog-work/` beside the source log unless `--out` says
otherwise. The source log is never modified.

## Changing this skill

Regenerate the fixtures and re-run the checks before trusting an edit to
`nrlog.py`:

```
python tests/make_fixtures.py && python tests/make_merge_fixtures.py \
  && python tests/make_playbook_fixtures.py && python -m unittest discover -s tests
```

Run all three generators. A fixture-dependent test skips when its fixtures are
absent, so a green run over missing fixtures proves nothing.

They cover a restart, a rolled sibling, a truncated session, interleaved pids,
one pid hosting three app domains, a runtime level change, DEBUG payload lines,
exception continuation lines, a planted license key for the redaction check, a
field-tier playbook, and playbook sets large enough to exercise the report caps.
Generated fixtures are gitignored.

`tests/golden/triage-basic.txt` records the shape of the triage report, and one
test compares the live report against it. A deliberate change to that shape means
re-recording the golden with `python tests/record_golden.py` and reading the diff
line by line before accepting it. A golden failure you did not intend is a defect
in the change, not a reason to re-record.

## References

- [references/log-formats.md](references/log-formats.md) - line layouts, level
  names, session identity, what each file is named and where it lives. Read
  before hand-parsing anything the script does not cover.
- [references/collector-protocol.md](references/collector-protocol.md) -
  endpoints, the healthy call sequence, connect request and response fields,
  positional array keys. Read when interpreting a payload or a response.
- [references/playbooks/](references/playbooks/) - one file per symptom, each
  with its verified signature, its verdict, the customer fix, and the next ask.
  `triage` names the files it matched, so there is no need to browse the
  directory. [references/playbooks/README.md](references/playbooks/README.md)
  documents the file format for adding one.
