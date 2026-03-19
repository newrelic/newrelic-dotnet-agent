#!/usr/bin/env bash
# run-perf-tests.sh
#
# Runs the .NET agent performance test suite locally or from CI.
# Must be run from the directory containing docker-compose.yml, or invoked
# from anywhere — the script always cd's to its own directory first.
#
# Usage: ./run-perf-tests.sh [options]
#
# Options:
#   --attach-agent true|false   Attach the New Relic agent (default: false)
#   --agent-home PATH           Agent home directory
#                               Locally defaults to $CORECLR_NEWRELIC_HOME
#   --app-name NAME             New Relic app name (default: dotnet-agent-perf-test-local)
#   --test-duration DURATION    Locust --run-time value, e.g. "2m" (default: 2m)
#   --locust-users N            Concurrent Locust users (default: 10)
#   --locust-spawn-rate N       Users spawned per second (default: 2)
#   --dotnet-version VER        .NET version for the test app container (default: 10.0)
#   --license-key KEY           New Relic license key (default: $NEW_RELIC_LICENSE_KEY)
#   --collector-host HOST       New Relic collector host (default: $NEW_RELIC_HOST)
#   --verbose true|false        Verbose output including test app and traffic driver output from container (default: false)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
ATTACH_AGENT="false"
AGENT_HOME=""
APP_NAME="dotnet-agent-perf-test-local"
TEST_DURATION="2m"
LOCUST_USERS="10"
LOCUST_SPAWN_RATE="2"
DOTNET_VERSION="10.0"
LICENSE_KEY="${NEW_RELIC_LICENSE_KEY:-}"
COLLECTOR_HOST="${NEW_RELIC_HOST:-}"
VERBOSE="false"

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    --attach-agent)      ATTACH_AGENT="$2";      shift 2 ;;
    --agent-home)        AGENT_HOME="$2";         shift 2 ;;
    --app-name)          APP_NAME="$2";           shift 2 ;;
    --test-duration)     TEST_DURATION="$2";      shift 2 ;;
    --locust-users)      LOCUST_USERS="$2";       shift 2 ;;
    --locust-spawn-rate) LOCUST_SPAWN_RATE="$2";  shift 2 ;;
    --dotnet-version)    DOTNET_VERSION="$2";     shift 2 ;;
    --license-key)       LICENSE_KEY="$2";        shift 2 ;;
    --collector-host)    COLLECTOR_HOST="$2";     shift 2 ;;
    --verbose)           VERBOSE="$2";     shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

# ---------------------------------------------------------------------------
# Validate inputs
# ---------------------------------------------------------------------------
if [ "$ATTACH_AGENT" = "true" ]; then
  if [ -z "$AGENT_HOME" ]; then
    if [ -n "${CORECLR_NEWRELIC_HOME:-}" ]; then
      AGENT_HOME="$CORECLR_NEWRELIC_HOME"
      echo "Using agent home from CORECLR_NEWRELIC_HOME: $AGENT_HOME"
    else
      echo "ERROR: --attach-agent is true but --agent-home was not specified and CORECLR_NEWRELIC_HOME is not set." >&2
      exit 1
    fi
  fi
  if [ -z "$LICENSE_KEY" ]; then
    echo "ERROR: --attach-agent is true but no license key was provided (--license-key or NEW_RELIC_LICENSE_KEY)." >&2
    exit 1
  fi
fi

# ---------------------------------------------------------------------------
# Summary helper
# Pipes stdin to stdout; also appends to GITHUB_STEP_SUMMARY when in CI.
# Usage: { echo "line1"; echo "line2"; } | to_summary
# ---------------------------------------------------------------------------
to_summary() {
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    tee -a "$GITHUB_STEP_SUMMARY"
  else
    cat
  fi
}

# ---------------------------------------------------------------------------
# Directories
# ---------------------------------------------------------------------------
mkdir -p logs results

# ---------------------------------------------------------------------------
# Docker Compose environment
# ---------------------------------------------------------------------------
export DOTNET_VERSION
export TEST_DURATION
export LOCUST_USERS
export LOCUST_SPAWN_RATE
export NEW_RELIC_LICENSE_KEY="${LICENSE_KEY:-}"
export NEW_RELIC_HOST="${COLLECTOR_HOST:-}"
export NEW_RELIC_APP_NAME="$APP_NAME"

if [ "$ATTACH_AGENT" = "true" ]; then
  export AGENT_PATH="$AGENT_HOME"
  export CORECLR_ENABLE_PROFILING=1
else
  mkdir -p agent-home
  export AGENT_PATH="$SCRIPT_DIR/agent-home"
  export CORECLR_ENABLE_PROFILING=0
fi

# ---------------------------------------------------------------------------
# Cleanup trap — always runs on exit
# ---------------------------------------------------------------------------
DOCKER_STATS_PID=""

cleanup() {
  if [ -n "$DOCKER_STATS_PID" ]; then
    kill "$DOCKER_STATS_PID" 2>/dev/null || true
    DOCKER_STATS_PID=""
  fi
  docker compose down --volumes --remove-orphans 2>/dev/null || true
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
# Build and start containers
# ---------------------------------------------------------------------------
echo "Building Docker images..."
docker compose build

echo "Starting test app and traffic driver..."
docker compose up -d

echo "Waiting for test app to become healthy..."
timeout 60 bash -c 'until docker inspect perf-testapp --format="{{.State.Health.Status}}" 2>/dev/null | grep -q "healthy"; do sleep 2; done'
echo "Test app is healthy. Traffic driver is running."

# ---------------------------------------------------------------------------
# Docker stats monitoring
# ---------------------------------------------------------------------------
STATS_FILE="results/docker-stats.csv"
echo "timestamp,cpu_pct,mem_pct,mem_usage,net_io,block_io,pids" > "$STATS_FILE"

while true; do
  TS=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  LINE=$(docker stats perf-testapp --no-stream \
    --format "{{.CPUPerc}},{{.MemPerc}},{{.MemUsage}},{{.NetIO}},{{.BlockIO}},{{.PIDs}}" \
    2>/dev/null || true)
  [ -n "$LINE" ] && echo "${TS},${LINE}" >> "$STATS_FILE"
  sleep 5
done &

DOCKER_STATS_PID=$!
echo "Docker stats monitoring started (PID $DOCKER_STATS_PID)."

# ---------------------------------------------------------------------------
# Wait for traffic driver to finish
# ---------------------------------------------------------------------------
echo "Waiting for traffic driver to finish..."
TRAFFIC_EXIT_CODE=$(docker wait perf-traffic-driver)
echo "Traffic driver exited with code: $TRAFFIC_EXIT_CODE"

# ---------------------------------------------------------------------------
# Stop Docker stats monitoring
# ---------------------------------------------------------------------------
kill "$DOCKER_STATS_PID" 2>/dev/null || true
DOCKER_STATS_PID=""
STATS_SAMPLES=$(( $(wc -l < "$STATS_FILE") - 1 ))
echo "Docker stats monitoring stopped. Collected $STATS_SAMPLES sample(s)."

# ---------------------------------------------------------------------------
# Capture container logs
# ---------------------------------------------------------------------------
docker logs perf-testapp >logs/testapp-stdout.log 2>&1 || true
docker logs perf-traffic-driver >logs/traffic-driver.log 2>&1 || true
if [ "$VERBOSE" == "true" ]; then
  echo "=== Test app logs ==="
  cat logs/testapp-stdout.log
  echo "=== Traffic driver logs ==="
  cat logs/traffic-driver.log
fi

# Bring down containers now; cleanup trap will also run on EXIT but explicit
# teardown here ensures containers are stopped before log analysis.
echo "Stopping containers..."
docker compose down --volumes --remove-orphans || true
echo "Containers stopped."

# ---------------------------------------------------------------------------
# Check test app for errors
# ---------------------------------------------------------------------------
ERRORS=0

if [ -f "logs/testapp-stdout.log" ]; then
  if grep -qi "unhandled exception\|application crashed\|fail to start" "logs/testapp-stdout.log"; then
    echo "ERROR: Test app logged a critical error:"
    grep -i "unhandled exception\|application crashed\|fail to start" "logs/testapp-stdout.log"
    ERRORS=$((ERRORS + 1))
  fi
fi

if [ "$ATTACH_AGENT" = "true" ]; then
  for LOG_FILE in logs/newrelic_agent*.log; do
    if [ -f "$LOG_FILE" ] && grep -q "FATAL ERROR" "$LOG_FILE"; then
      echo "ERROR: Agent log contains FATAL ERROR entries in $LOG_FILE:"
      grep "FATAL ERROR" "$LOG_FILE"
      ERRORS=$((ERRORS + 1))
    fi
  done
fi

if [ "$ERRORS" -gt 0 ]; then
  echo "FAIL: Critical errors found in test app or agent logs."
  exit 1
fi
echo "No critical errors found in test app or agent logs."

# ---------------------------------------------------------------------------
# Check traffic driver exit code
# ---------------------------------------------------------------------------
if [ "$TRAFFIC_EXIT_CODE" != "0" ]; then
  echo "ERROR: Traffic driver exited with code $TRAFFIC_EXIT_CODE (error rate exceeded threshold)"
  if [ -f "results/locust_stats.csv" ] && [ "$VERBOSE" == "true" ]; then
    echo "--- Locust stats ---"
    cat "results/locust_stats.csv"
  fi
  exit 1
fi
echo "Traffic driver completed successfully."

# ---------------------------------------------------------------------------
# Verify agent connected to New Relic (agent runs only)
# ---------------------------------------------------------------------------
if [ "$ATTACH_AGENT" = "true" ]; then
  CONNECTED=0
  for LOG_FILE in logs/newrelic_agent*.log; do
    if [ -f "$LOG_FILE" ]; then
      if grep -q 'Agent fully connected\.' "$LOG_FILE"; then
        CONNECTED=1
        break
      fi
    fi
  done

  if [ "$CONNECTED" -eq 0 ]; then
    echo "ERROR: Agent did not fully connect (no 'Agent fully connected.' line found in agent logs)"
    ls -la logs/newrelic_agent*.log 2>/dev/null || echo "  (no agent log files found)"
    exit 1
  fi
  echo "SUCCESS: Agent fully connected to New Relic"
fi

# ---------------------------------------------------------------------------
# Print summary
# ---------------------------------------------------------------------------
{
  if [ -f "results/locust_stats.csv" ]; then
    echo "### Locust Results"
    echo ""
    "${PYTHON:-python3}" - << 'PYEOF'
import csv, sys

# Columns: Name(1), Avg RT(5), Req/s(9), 50%(11), 75%(13), 95%(16), 99%(18), 100%(21)
COLS = [1, 5, 9, 11, 13, 16, 18, 21]

try:
    with open("results/locust_stats.csv", newline="") as f:
        rows = list(csv.reader(f))
except FileNotFoundError:
    sys.exit(0)

if len(rows) < 2:
    sys.exit(0)

header = [rows[0][i] for i in COLS]
print("| " + " | ".join(header) + " |")
print("| " + " | ".join(["---"] * len(COLS)) + " |")
for row in rows[1:]:
    if len(row) > max(COLS):
        print("| " + " | ".join(row[i] for i in COLS) + " |")
PYEOF
  else
    echo "No Locust stats CSV found."
  fi

  if [ "$STATS_SAMPLES" -gt 0 ]; then
    echo ""
    echo "### Docker Stats ($STATS_SAMPLES samples)"
    echo ""
    "${PYTHON:-python3}" - << 'PYEOF'
import csv, sys

try:
    with open("results/docker-stats.csv", newline="") as f:
        rows = list(csv.reader(f))
except FileNotFoundError:
    sys.exit(0)

if len(rows) < 2:
    sys.exit(0)

header = rows[0]
print("| " + " | ".join(header) + " |")
print("| " + " | ".join(["---"] * len(header)) + " |")
for row in rows[1:]:
    print("| " + " | ".join(row) + " |")
PYEOF
  fi

  echo ""
  echo "**Run configuration:**"
  echo "- Agent attached: $ATTACH_AGENT"
  echo "- Test duration: $TEST_DURATION"
  echo "- Locust users: $LOCUST_USERS"
  echo "- .NET version: $DOTNET_VERSION"
  if [ "$ATTACH_AGENT" = "true" ]; then
    echo "- NR App name: $APP_NAME"
    [ -n "$COLLECTOR_HOST" ] && echo "- Collector host: $COLLECTOR_HOST"
  fi
} | to_summary

echo "Performance test run complete."
