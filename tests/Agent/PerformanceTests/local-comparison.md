# Local Performance Comparison Script

## Context

The `compare_performance.yml` workflow enables multi-run comparisons in CI, but there's no equivalent for local development. A local comparison script would let developers quickly benchmark agent versions (or no-agent vs agent) before pushing, using the same `run-perf-tests.sh` and `ReportGenerator` infrastructure. The script accepts a YAML config file defining runs so the comparison is reproducible and easy to share.

---

## Files to Create

### 1. `tests/Agent/PerformanceTests/compare-perf-runs.py` (NEW)

Python 3 script. Run from the `PerformanceTests/` directory or anywhere (it `chdir`s to its own directory). Requires PyYAML (`pip install pyyaml`) and Docker.

**Usage:**
```
python compare-perf-runs.py [--config FILE] [--results-dir DIR] [--no-report]
  --config FILE        YAML config file (default: compare.yml)
  --results-dir DIR    Where to store per-run results (default: comparison-results/<timestamp>)
  --no-report          Skip ReportGenerator step
```

**Config file structure** (see `compare.example.yml`):
```yaml
test:
  duration: 2m
  locust_users: 10
  locust_spawn_rate: 2
  dotnet_version: "10.0"

runs:
  - label: no-agent
    attach_agent: false

  - label: local-dev
    attach_agent: true
    agent_source:
      type: local
      path: /path/to/newrelichome_x64_coreclr_linux

  - label: released-10.49.0
    attach_agent: true
    agent_source:
      type: url
      url: https://download.newrelic.com/dot_net_agent/latest_release/newrelic-dotnet-agent_10.49.0_amd64.tar.gz

  - label: pr-build
    attach_agent: true
    agent_source:
      type: github_artifact
      run_id: 12345678
      # OR: run_url: https://github.com/newrelic/newrelic-dotnet-agent/actions/runs/12345678
```

**Script logic (sequential):**

For each run:
1. Create a temp agent-home directory: `comparison-results/<timestamp>/agent-home-<label>/`
2. Populate it based on `agent_source.type`:
   - `local`: `shutil.copytree(path, agent_home)`
   - `url`: download to temp file (supports `.zip`, `.tar.gz`, `.tgz`), extract, find `newrelic-dotnet-agent` subdir, copy contents
   - `github_artifact`: run `gh run download <run_id> --name homefolders --dir <tmpdir>`, find `newrelichome_x64_coreclr_linux`, copy contents
   - For `github_artifact` with `run_url`: parse the run ID from the URL using regex `r'/runs/(\d+)'`
3. Build results directory: `comparison-results/<timestamp>/perf-results-<label>/`
4. Call `run-perf-tests.sh` via `subprocess.run()`:
   ```
   bash run-perf-tests.sh
     --attach-agent <true|false>
     --agent-home   <agent_home>
     --app-name     dotnet-agent-perf-test-local-<label>
     --test-duration <duration>
     --locust-users  <N>
     --locust-spawn-rate <N>
     --dotnet-version <VER>
   ```
5. After each run completes, move `results/` contents to `comparison-results/<timestamp>/perf-results-<label>/` and `logs/` contents to `comparison-results/<timestamp>/logs-<label>/` before the next run begins.
6. Print per-run pass/fail summary.

After all runs:
- If `--no-report` not set and Docker is available (`docker info` exit code 0):
  - Build ReportGenerator image: `docker build -t perf-report-generator ReportGenerator/`
  - Run it:
    ```
    docker run --rm
      -v <timestamp_dir>:/input:ro
      -v <timestamp_dir>/report:/output
      perf-report-generator
      --input-dir /input
      --output-dir /output
    ```
  - Print path to `report/summary.md`
- If Docker unavailable: print warning, skip report.

**Error handling:**
- If a run fails (non-zero exit from `run-perf-tests.sh`): print error, continue to next run, note the failure in final summary.
- If `gh` CLI not found for `github_artifact`: exit with clear message.
- If `pyyaml` not installed: include `pip install pyyaml` hint in error message.

---

### 2. `tests/Agent/PerformanceTests/compare.example.yml` (NEW)

Fully-commented example config showing all four source types:

```yaml
# compare.example.yml — copy to compare.yml and edit for your run.
#
# Run with: python compare-perf-runs.py
#   or:     python compare-perf-runs.py --config my-comparison.yml

test:
  duration: 2m          # Locust --run-time (e.g. "2m", "5m", "30s")
  locust_users: 10      # Concurrent Locust users
  locust_spawn_rate: 2  # Users to spawn per second
  dotnet_version: "10.0"

runs:
  # --- Baseline: no agent ---
  - label: no-agent
    attach_agent: false

  # --- Local agent home folder ---
  - label: local-build
    attach_agent: true
    agent_source:
      type: local
      path: /path/to/newrelichome_x64_coreclr_linux   # absolute or relative path

  # --- Downloaded zip or tarball ---
  - label: released
    attach_agent: true
    agent_source:
      type: url
      url: https://download.newrelic.com/dot_net_agent/latest_release/newrelic-dotnet-agent_10.49.0_amd64.tar.gz
      # Supports .zip, .tar.gz, .tgz

  # --- GitHub Actions build artifact ---
  - label: pr-build
    attach_agent: true
    agent_source:
      type: github_artifact
      # Either run_id (number) or run_url (copy from browser) — not both
      run_id: 12345678
      # run_url: https://github.com/newrelic/newrelic-dotnet-agent/actions/runs/12345678
```

---

## Dependencies

- **Python 3** with `pyyaml` (`pip install pyyaml`)
- **Docker** (for running test containers and optionally the report generator)
- **`gh` CLI** (only for `github_artifact` source type, must be authenticated)
- **`bash`** (Git Bash on Windows, native on Linux/macOS)
- Existing `run-perf-tests.sh` at `tests/Agent/PerformanceTests/`
- Existing `ReportGenerator/Dockerfile` at `tests/Agent/PerformanceTests/ReportGenerator/`

---

## Verification

1. `python compare-perf-runs.py --config compare.example.yml` — should fail gracefully with "path not found" errors (example paths aren't real)
2. Create a `compare.yml` with two `local` runs pointing to the same real agent home, run it — both should complete and produce a report in `comparison-results/<timestamp>/report/`
3. Try `--no-report` flag — should skip Docker report generation
4. Try a `github_artifact` run with a valid run URL — should parse run ID and download correctly
5. Kill mid-run (`Ctrl+C`) — Docker containers should be cleaned up by `run-perf-tests.sh`'s trap
