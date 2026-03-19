#!/usr/bin/env python
"""
run-perf-test.py

Runs the .NET agent performance test suite locally or from CI.
Must be run from the directory containing docker-compose.yml, or invoked
from anywhere — the script always cd's to its own directory first.

Usage: python run-perf-test.py [options]

Options:
  --attach-agent true|false   Attach the New Relic agent (default: false)
  --agent-home PATH           Agent home directory
                              Locally defaults to $CORECLR_NEWRELIC_HOME
  --app-name NAME             New Relic app name (default: dotnet-agent-perf-test-local)
  --test-duration DURATION    Locust --run-time value, e.g. "2m" (default: 2m)
  --locust-users N            Concurrent Locust users (default: 10)
  --locust-spawn-rate N       Users spawned per second (default: 2)
  --dotnet-version VER        .NET version for the test app container (default: 10.0)
  --license-key KEY           New Relic license key (default: $NEW_RELIC_LICENSE_KEY)
  --collector-host HOST       New Relic collector host (default: $NEW_RELIC_HOST)
  --env NAME=VALUE            Extra environment variable to forward into the test app
                              container. Repeatable; each --env adds one NAME=VALUE
                              pair to extra.env.
  --verbose true|false        Verbose output including container logs (default: false)
"""

import argparse
import csv
import glob
import os
import re
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))


# ---------------------------------------------------------------------------
# Argument parsing
# ---------------------------------------------------------------------------

def parse_args():
    parser = argparse.ArgumentParser(description="Run the .NET agent performance test suite.")
    parser.add_argument("--attach-agent",      default="false")
    parser.add_argument("--agent-home",         default="")
    parser.add_argument("--app-name",           default="dotnet-agent-perf-test-local")
    parser.add_argument("--test-duration",      default="2m")
    parser.add_argument("--locust-users",       default="10")
    parser.add_argument("--locust-spawn-rate",  default="2")
    parser.add_argument("--dotnet-version",     default="10.0")
    parser.add_argument("--license-key",        default=os.environ.get("NEW_RELIC_LICENSE_KEY", ""))
    parser.add_argument("--collector-host",     default=os.environ.get("NEW_RELIC_HOST", ""))
    parser.add_argument("--env",                action="append", default=[], dest="extra_envs", metavar="NAME=VALUE")
    parser.add_argument("--verbose",            default="false")
    return parser.parse_args()


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def run(cmd, **kwargs):
    """Run a command, raising CalledProcessError on failure."""
    return subprocess.run(cmd, check=True, **kwargs)


def run_output(cmd, **kwargs):
    """Run a command and return (stdout_stripped, returncode)."""
    result = subprocess.run(cmd, capture_output=True, text=True, **kwargs)
    return result.stdout.strip(), result.returncode


def to_summary(text):
    """Print text and also append to $GITHUB_STEP_SUMMARY when running in CI."""
    print(text)
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY", "")
    if summary_path:
        with open(summary_path, "a") as f:
            f.write(text + "\n")


def format_csv_as_markdown(csv_path, cols=None):
    """Return a markdown table from a CSV file, or empty string if unavailable."""
    try:
        with open(csv_path, newline="") as f:
            rows = list(csv.reader(f))
    except FileNotFoundError:
        return ""

    if len(rows) < 2:
        return ""

    if cols is not None:
        header = [rows[0][i] for i in cols if i < len(rows[0])]
        data_rows = [
            [row[i] for i in cols if i < len(row)]
            for row in rows[1:]
            if len(row) > max(cols)
        ]
    else:
        header = rows[0]
        data_rows = rows[1:]

    lines = [
        "| " + " | ".join(header) + " |",
        "| " + " | ".join(["---"] * len(header)) + " |",
    ]
    for row in data_rows:
        lines.append("| " + " | ".join(row) + " |")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Docker stats background thread
# ---------------------------------------------------------------------------

def _collect_docker_stats(stats_file, stop_event):
    while not stop_event.is_set():
        ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        result = subprocess.run(
            [
                "docker", "stats", "perf-testapp", "--no-stream",
                "--format", "{{.CPUPerc}},{{.MemPerc}},{{.MemUsage}},{{.NetIO}},{{.BlockIO}},{{.PIDs}}",
            ],
            capture_output=True, text=True,
        )
        if result.returncode == 0 and result.stdout.strip():
            with open(stats_file, "a") as f:
                f.write(f"{ts},{result.stdout.strip()}\n")
        stop_event.wait(5)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    os.chdir(SCRIPT_DIR)
    args = parse_args()

    attach_agent = args.attach_agent.lower() == "true"
    verbose = args.verbose.lower() == "true"

    # --- Validate inputs ---
    agent_home = args.agent_home
    if attach_agent:
        if not agent_home:
            coreclr_home = os.environ.get("CORECLR_NEWRELIC_HOME", "")
            if coreclr_home:
                agent_home = coreclr_home
                print(f"Using agent home from CORECLR_NEWRELIC_HOME: {agent_home}")
            else:
                print(
                    "ERROR: --attach-agent is true but --agent-home was not specified "
                    "and CORECLR_NEWRELIC_HOME is not set.",
                    file=sys.stderr,
                )
                sys.exit(1)
        if not args.license_key:
            print(
                "ERROR: --attach-agent is true but no license key was provided "
                "(--license-key or NEW_RELIC_LICENSE_KEY).",
                file=sys.stderr,
            )
            sys.exit(1)

    # --- Directories ---
    os.makedirs("logs", exist_ok=True)
    os.makedirs("results", exist_ok=True)

    # --- Docker Compose environment ---
    compose_env = os.environ.copy()
    compose_env.update({
        "DOTNET_VERSION":        args.dotnet_version,
        "TEST_DURATION":         args.test_duration,
        "LOCUST_USERS":          args.locust_users,
        "LOCUST_SPAWN_RATE":     args.locust_spawn_rate,
        "NEW_RELIC_LICENSE_KEY": args.license_key,
        "NEW_RELIC_HOST":        args.collector_host,
        "NEW_RELIC_APP_NAME":    args.app_name,
    })
    if attach_agent:
        compose_env["AGENT_PATH"] = agent_home
        compose_env["CORECLR_ENABLE_PROFILING"] = "1"
    else:
        os.makedirs("agent-home", exist_ok=True)
        compose_env["AGENT_PATH"] = os.path.join(SCRIPT_DIR, "agent-home")
        compose_env["CORECLR_ENABLE_PROFILING"] = "0"

    # --- Write extra.env for Docker Compose env_file injection ---
    with open("extra.env", "w") as f:
        for pair in args.extra_envs:
            f.write(pair + "\n")

    # --- Cleanup (runs on normal exit and on unhandled exceptions) ---
    stats_stop = threading.Event()

    def cleanup():
        stats_stop.set()
        subprocess.run(
            ["docker", "compose", "down", "--volumes", "--remove-orphans"],
            capture_output=True,
            env=compose_env,
        )

    import atexit
    atexit.register(cleanup)

    # --- Build and start containers ---
    print("Building Docker images...")
    run(["docker", "compose", "build"], env=compose_env)

    print("Starting test app and traffic driver...")
    run(["docker", "compose", "up", "-d"], env=compose_env)

    # --- Wait for testapp to become healthy ---
    print("Waiting for test app to become healthy...")
    deadline = time.monotonic() + 60
    while True:
        health, _ = run_output(
            ["docker", "inspect", "perf-testapp", "--format", "{{.State.Health.Status}}"]
        )
        if health == "healthy":
            break
        if time.monotonic() > deadline:
            print("ERROR: Test app did not become healthy within 60 seconds.", file=sys.stderr)
            sys.exit(1)
        time.sleep(2)
    print("Test app is healthy. Traffic driver is running.")

    # --- Docker stats monitoring ---
    stats_file = "results/docker-stats.csv"
    with open(stats_file, "w") as f:
        f.write("timestamp,cpu_pct,mem_pct,mem_usage,net_io,block_io,pids\n")

    stats_thread = threading.Thread(
        target=_collect_docker_stats, args=(stats_file, stats_stop), daemon=True
    )
    stats_thread.start()
    print("Docker stats monitoring started.")

    # --- Wait for traffic driver to finish ---
    print("Waiting for traffic driver to finish...")
    traffic_exit_code, _ = run_output(["docker", "wait", "perf-traffic-driver"])
    print(f"Traffic driver exited with code: {traffic_exit_code}")

    # --- Stop stats monitoring ---
    stats_stop.set()
    stats_thread.join(timeout=10)
    with open(stats_file) as f:
        stats_samples = max(0, sum(1 for _ in f) - 1)
    print(f"Docker stats monitoring stopped. Collected {stats_samples} sample(s).")

    # --- Capture container logs ---
    for container, log_path in [
        ("perf-testapp",      "logs/testapp-stdout.log"),
        ("perf-traffic-driver", "logs/traffic-driver.log"),
    ]:
        result = subprocess.run(["docker", "logs", container], capture_output=True, text=True)
        with open(log_path, "w") as f:
            f.write(result.stdout)
            f.write(result.stderr)

    if verbose:
        print("=== Test app logs ===")
        with open("logs/testapp-stdout.log") as f:
            print(f.read(), end="")
        print("=== Traffic driver logs ===")
        with open("logs/traffic-driver.log") as f:
            print(f.read(), end="")

    # --- Stop containers ---
    print("Stopping containers...")
    subprocess.run(
        ["docker", "compose", "down", "--volumes", "--remove-orphans"],
        env=compose_env,
    )
    print("Containers stopped.")

    # --- Check test app and agent logs for critical errors ---
    errors = 0
    critical_pattern = re.compile(
        r"unhandled exception|application crashed|fail to start", re.IGNORECASE
    )

    if os.path.isfile("logs/testapp-stdout.log"):
        with open("logs/testapp-stdout.log") as f:
            content = f.read()
        bad_lines = [line for line in content.splitlines() if critical_pattern.search(line)]
        if bad_lines:
            print("ERROR: Test app logged a critical error:")
            print("\n".join(bad_lines))
            errors += 1

    if attach_agent:
        for log_file in glob.glob("logs/newrelic_agent*.log"):
            with open(log_file) as f:
                content = f.read()
            fatal_lines = [line for line in content.splitlines() if "FATAL ERROR" in line]
            if fatal_lines:
                print(f"ERROR: Agent log contains FATAL ERROR entries in {log_file}:")
                print("\n".join(fatal_lines))
                errors += 1

    if errors > 0:
        print("FAIL: Critical errors found in test app or agent logs.")
        sys.exit(1)
    print("No critical errors found in test app or agent logs.")

    # --- Check traffic driver exit code ---
    if traffic_exit_code != "0":
        print(f"ERROR: Traffic driver exited with code {traffic_exit_code} (error rate exceeded threshold)")
        if verbose and os.path.isfile("results/locust_stats.csv"):
            print("--- Locust stats ---")
            with open("results/locust_stats.csv") as f:
                print(f.read(), end="")
        sys.exit(1)
    print("Traffic driver completed successfully.")

    # --- Verify agent connected to New Relic (agent runs only) ---
    if attach_agent:
        connected = any(
            "Agent fully connected." in open(log_file).read()
            for log_file in glob.glob("logs/newrelic_agent*.log")
        )
        if not connected:
            print("ERROR: Agent did not fully connect (no 'Agent fully connected.' line found in agent logs)")
            agent_logs = glob.glob("logs/newrelic_agent*.log")
            if agent_logs:
                for p in agent_logs:
                    print(f"  {p}")
            else:
                print("  (no agent log files found)")
            sys.exit(1)
        print("SUCCESS: Agent fully connected to New Relic")

    # --- Print summary ---
    summary_lines = []

    # Locust stats table — columns: Name(1), Avg RT(5), Req/s(9), 50%(11), 75%(13), 95%(16), 99%(18), 100%(21)
    locust_table = format_csv_as_markdown("results/locust_stats.csv", cols=[1, 5, 9, 11, 13, 16, 18, 21])
    if locust_table:
        summary_lines += ["### Locust Results", "", locust_table]
    else:
        summary_lines.append("No Locust stats CSV found.")

    if stats_samples > 0:
        docker_table = format_csv_as_markdown(stats_file)
        if docker_table:
            summary_lines += ["", f"### Docker Stats ({stats_samples} samples)", "", docker_table]

    summary_lines += [
        "",
        "**Run configuration:**",
        f"- Agent attached: {args.attach_agent}",
        f"- Test duration: {args.test_duration}",
        f"- Locust users: {args.locust_users}",
        f"- .NET version: {args.dotnet_version}",
    ]
    if attach_agent:
        summary_lines.append(f"- NR App name: {args.app_name}")
        if args.collector_host:
            summary_lines.append(f"- Collector host: {args.collector_host}")
    if args.extra_envs:
        summary_lines.append("- Extra environment variables:")
        for pair in args.extra_envs:
            summary_lines.append(f"  - {pair}")

    to_summary("\n".join(summary_lines))

    print("Performance test run complete.")


if __name__ == "__main__":
    main()
