# MetricNameDiscovery

Reflects over the built `NewRelic.Agent.Core.dll` to enumerate every
`Supportability/*` metric name that can be statically derived from
`MetricNames.cs`, and optionally diffs the result against Angler's
`metric_names.txt` to surface candidates for addition.

## Prerequisites

- `FullAgent.sln` must have been built at least once so that
  `src/Agent/newrelichome_x64_coreclr/` exists. The tool loads
  `NewRelic.Agent.Core.dll` from that directory at runtime.

## Usage

```
dotnet run --project build/MetricNameDiscovery/MetricNameDiscovery.csproj -- [options]
```

| Option | Description |
|---|---|
| `--agent-home <path>` | Path to an agent home directory containing `NewRelic.Agent.Core.dll`. Defaults to `src/Agent/newrelichome_x64_coreclr` relative to the repo root. |
| `--diff <angler-file>` | Path to a local copy of Angler's `metric_names.txt`. Adds a `=== Candidate additions ===` section listing all metrics present in code but absent from the file and not in the exclusions list. |
| `--exclusions <path>` | Path to an exclusions file. Defaults to `build/MetricNameDiscovery/exclusions.txt` if it exists. |

## Output sections

- **Discovered Supportability metrics** - every metric name the tool could
  enumerate from fields, properties, and methods with enum/bool parameters.
- **Methods not enumerated** - methods whose parameters include free-form
  strings (shapes C/D); these cannot be enumerated without runtime data and
  are listed for manual review.
- **Candidate additions** (diff mode only) - all metrics in code that are
  absent from the Angler file and not in the exclusions list.

## Exclusions file

`build/MetricNameDiscovery/exclusions.txt` lists metric names that should
never appear as candidates. Add an entry when a metric is intentionally
absent from Angler (e.g. internal-only, redundant, or not yet ready).
Blank lines and lines starting with `#` are ignored.

## Integration with the Angler release skill

The `angler-dotnet-release` skill runs this tool automatically during Step 2
and presents any candidates to the user for classification before opening the
Angler PR. See `.claude/skills/angler-dotnet-release/skill.md`.
