# Playbook file format

One file per symptom. `nrlog.py triage` loads every `*.md` file in this
directory except this README, matches its signatures against a session, and
names the files it matched. Read a playbook when triage names it; there is no
need to browse the directory.

`nrlog.py` refuses to load an invalid file and exits with the field and the file
name, so a format error surfaces on the next run instead of silently disabling a
playbook.

## Frontmatter

The block between the opening and closing `---` lines. The parser is a
restricted dialect, not YAML. It accepts `key: scalar`, `key: [a, b]`, and a
`key:` header followed by indented `  - item` lines. Nothing else.

| Field | Required | Value |
|---|---|---|
| `id` | yes | Integer, unique across the directory. Ids 1 to 99 are the shipped set and are `tier: verified`; a drafted `tier: field` playbook starts at 100. The validator enforces both ranges. |
| `title` | yes | One short noun phrase. It is the label in the report. |
| `tier` | yes | `verified` or `field`. `verified` means the signature was read from agent source. `field` means it was observed in a customer log. |
| `scope` | yes | `managed`, `profiler`, or `both`. Which log the signatures are searched in. Derive it from source: every signature a profiler string means `profiler`, every one a managed-agent string means `managed`, and only a genuinely mixed set gets `both`. |
| `min_level` | yes | The lowest level at which absence of every signature is conclusive. For each cause the body documents, take the least demanding signature that covers it; `min_level` is the highest level among those. A level name or an alias, for example `info`, `debug`, `finest`, `all`. The matcher gates only `managed` and `both` playbooks on it, so on a `profiler` playbook it documents the profiler log's level and changes no verdict. A playbook that matches on `level_max` or `stated_differs_from_observed` is evaluated before the gate and is never blocked by it, so its `min_level` value has no effect either. |
| `precedence` | yes | Integer. Display order in the report, ascending, tie-broken by `id`. |
| `signatures` | yes | List of literal substrings. May be empty only when `level_max` or `stated_differs_from_observed` is set. |
| `keywords` | yes | Three to five lowercase terms. They drive the changelog filter. |
| `level_max` | no | A level name. The playbook matches when the session's observed level is at or below this level, with no signature needed. |
| `stated_differs_from_observed` | no | `true` or `false`. The playbook matches when the stated level from the startup banner differs from the observed level, with no signature needed. When the banner is absent the playbook is blocked, because the log cannot answer the question. |
| `verified_versions` | for `tier: verified` | The agent release whose source the signatures were read from, for example `"10.54.0"`. A range is written only when someone checked both endpoints. |
| `observed_in` | for `tier: field` | Where the signature was observed, for example a ticket number. |

## Signatures

A signature is a literal substring that a real log line contains. The matcher
tests `signature in line`; it does not compile a regex.

- Take the longest run of invariant text from the source string. Cut it at the
  first `{placeholder}` or sample value, then strip trailing whitespace and
  trailing punctuation.
- A signature means presence of a fault. A success string, such as `Profiler
  initialized` or `Agent fully connected.`, never goes in `signatures:`; it
  belongs in the body prose, where it tells the engineer to move on.
- The validator rejects `{`, `}`, `.*`, `.+`, `\d`, `\w`, `\s`, `[0-9]`, `|`, a
  leading `^`, and a trailing `$`. Parentheses and Windows path backslashes are
  literal text and are allowed.
- Trimmed too short, a signature over-matches and the playbook reports a false
  positive. Trimmed too long, it under-matches. The report prints the matched
  line, so either error is visible on the first real run.

## Body

Free markdown, with two required headings:

- `## Customer fix` - what the customer changes, addressed to them, with no
  internal jargon.
- `## Next ask` - what to request when the log cannot settle the question. `None.`
  plus the one-sentence reason is a complete answer.

Links to a sibling reference file go up one directory, for example
[../log-formats.md](../log-formats.md) and
[../collector-protocol.md](../collector-protocol.md).
