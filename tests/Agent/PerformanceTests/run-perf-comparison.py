#!/usr/bin/env python
"""
run-perf-comparison.py

Run sequential performance test comparisons locally using run-perf-test.py.
Results are organized under a timestamped directory and optionally combined
into a report using the ReportGenerator Docker image.

Usage:
    python run-perf-comparison.py [--config FILE] [--results-dir DIR] [--no-report]
                                   [--agent-app-name NAME] [--add-label-to-app-name]

Requirements:
    pip install pyyaml
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
import tarfile
import urllib.request
from datetime import datetime

try:
    import yaml
except ImportError:
    print("ERROR: PyYAML is required. Install it with: pip install pyyaml", file=sys.stderr)
    sys.exit(1)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
GH_REPO = "newrelic/newrelic-dotnet-agent"  # for 'gh' CLI context when downloading artifacts

_BASH_IS_WSL2 = None


def _bash_type_is_wsl2():
    """Return True if 'bash' resolves to WSL2 bash rather than Git Bash (MINGW).

    From PowerShell (and cmd.exe), 'bash' typically resolves to the WSL2 launcher
    rather than Git Bash.  The two environments need different treatment:
    - WSL2 bash cannot execute Windows paths like C:/Python/python.exe.
    - Git Bash (MINGW) cannot use WSL2-internal paths like /mnt/c/...
    """
    global _BASH_IS_WSL2  # pylint: disable=global-statement
    if _BASH_IS_WSL2 is None:
        try:
            result = subprocess.run(
                ["bash", "-c", "uname -r"],
                capture_output=True, text=True, timeout=5, check=False
            )
            _BASH_IS_WSL2 = "microsoft" in result.stdout.lower()
        except Exception:  # pylint: disable=broad-exception-caught
            _BASH_IS_WSL2 = False
    return _BASH_IS_WSL2


def to_bash_path(path):
    """Normalise a path for use as a Docker volume mount source on Windows.

    os.path.join on Windows produces backslash paths (C:\\foo\\bar).  Simply
    converting to the Git Bash POSIX style (/c/foo/bar) is NOT sufficient for
    Docker volume mounts: MSYS path translation only applies to CLI *arguments*
    passed to Windows binaries, not to environment variable *values*.  When
    Docker Compose reads AGENT_PATH from the environment it receives the raw
    string; passing /c/foo/bar causes Docker (WSL2 backend) to treat it as a
    Linux absolute path that doesn't exist, resulting in an empty mount.

    Using the C:/foo/bar format (drive letter preserved, backslashes replaced
    with forward slashes) is unambiguously a Windows path to Docker Desktop
    regardless of invocation context, and is also accepted by Git Bash (MSYS2)
    for filesystem operations.
    """
    return os.path.abspath(path).replace("\\", "/")


def parse_args():
    """Parse and return command-line arguments."""
    parser = argparse.ArgumentParser(description="Run sequential performance test comparisons.")
    parser.add_argument("--config", default="compare.yml",
                        help="YAML config file (default: compare.yml)")
    parser.add_argument("--results-dir", default=None,
                        help="Where to store per-run results (default: comparison-results/<timestamp>)")
    parser.add_argument("--no-report", action="store_true",
                        help="Skip ReportGenerator step")
    parser.add_argument("--agent-app-name", default="dotnet-agent-perf-test",
                        help="Application name shown in New Relic (default: dotnet-agent-perf-test)")
    parser.add_argument("--add-label-to-app-name", action="store_true",
                        help=(
                            "Append run label to app name "
                            "(e.g. 'dotnet-agent-perf-test-local-foo'). "
                            "Useful for distinguishing runs in New Relic, "
                            "but may create many app names if labels are unique."
                        ))
    return parser.parse_args()


def load_config(config_path):
    """Load and return the YAML comparison config file."""
    if not os.path.isabs(config_path):
        config_path = os.path.join(SCRIPT_DIR, config_path)
    if not os.path.exists(config_path):
        print(f"ERROR: Config file not found: {config_path}", file=sys.stderr)
        sys.exit(1)
    with open(config_path, encoding="utf-8") as f:
        return yaml.safe_load(f)


def prepare_agent_home_local(source, agent_home_dir):
    """Copy a local agent home directory into agent_home_dir."""
    path = source.get("path", "")
    if not os.path.isabs(path):
        path = os.path.join(SCRIPT_DIR, path)
    if not os.path.isdir(path):
        raise FileNotFoundError(f"Local agent path not found: {path}")
    print(f"Copying local agent home dir from {path} to {agent_home_dir}")
    shutil.copytree(path, agent_home_dir, dirs_exist_ok=True)


def prepare_agent_home_url(source, agent_home_dir):
    """Download an agent archive from a URL and extract it into agent_home_dir."""
    url = source.get("url", "")
    if not url:
        raise ValueError("agent_source.url is required for type 'url'")

    suffix = ""
    lower_url = url.lower()
    if lower_url.endswith(".zip"):
        suffix = ".zip"
    elif lower_url.endswith(".tar.gz") or lower_url.endswith(".tgz"):
        suffix = ".tar.gz"
    else:
        suffix = ".tar.gz"

    with tempfile.TemporaryDirectory() as tmp:
        archive_path = os.path.join(tmp, f"agent-download{suffix}")
        print(f"  Downloading {url} ...")
        urllib.request.urlretrieve(url, archive_path)

        extract_dir = os.path.join(tmp, "extracted")
        os.makedirs(extract_dir)

        if suffix == ".zip":
            with zipfile.ZipFile(archive_path) as zf:
                zf.extractall(extract_dir)
        else:
            with tarfile.open(archive_path) as tf:
                tf.extractall(extract_dir)

        # Find the agent directory: prefer 'newrelic-dotnet-agent', then 'newrelichome_x64_coreclr_linux'
        agent_subdir = None
        for candidate in ["newrelic-dotnet-agent", "newrelichome_x64_coreclr_linux"]:
            for root, dirs, _ in os.walk(extract_dir):
                if candidate in dirs:
                    agent_subdir = os.path.join(root, candidate)
                    break
            if agent_subdir:
                break

        if agent_subdir is None:
            agent_subdir = extract_dir

        shutil.copytree(agent_subdir, agent_home_dir, dirs_exist_ok=True)


def prepare_agent_home_github_artifact(source, agent_home_dir):
    """Download an agent artifact from a GitHub Actions run and extract it into agent_home_dir."""
    if shutil.which("gh") is None:
        print(
            "ERROR: 'gh' CLI not found. Install it and authenticate before using github_artifact source.",
            file=sys.stderr,
        )
        sys.exit(1)

    run_id = source.get("run_id")
    run_url = source.get("run_url")

    if run_url and not run_id:
        match = re.search(r"/runs/(\d+)", run_url)
        if not match:
            raise ValueError(f"Could not parse run ID from run_url: {run_url}")
        run_id = match.group(1)

    if not run_id:
        raise ValueError("agent_source requires either run_id or run_url for type 'github_artifact'")

    run_id = str(run_id)

    with tempfile.TemporaryDirectory() as tmp:
        print(f"  Downloading artifact from GitHub Actions run {run_id} ...")
        result = subprocess.run(
            ["gh", "-R", GH_REPO, "run", "download", run_id, "--name", "homefolders", "--dir", tmp],
            capture_output=True, text=True, check=False,
        )
        if result.returncode != 0:
            raise RuntimeError(f"gh run download failed:\n{result.stderr}")

        agent_subdir = None
        for candidate in ["newrelichome_x64_coreclr_linux", "newrelic-dotnet-agent"]:
            for root, dirs, _ in os.walk(tmp):
                if candidate in dirs:
                    agent_subdir = os.path.join(root, candidate)
                    break
            if agent_subdir:
                break

        if agent_subdir is None:
            agent_subdir = tmp

        shutil.copytree(agent_subdir, agent_home_dir, dirs_exist_ok=True)


def prepare_agent_home(run_cfg, agent_home_dir):
    """Prepare the agent home directory for a single run based on run config."""
    # Always start clean so files from a previous run's agent don't linger.
    if os.path.isdir(agent_home_dir):
        shutil.rmtree(agent_home_dir)
    os.makedirs(agent_home_dir)

    attach = run_cfg.get("attach_agent", False)
    if not attach:
        return  # leave the directory empty for no-agent runs

    source = run_cfg.get("agent_source", {})
    source_type = source.get("type", "local")

    if source_type == "local":
        prepare_agent_home_local(source, agent_home_dir)
    elif source_type == "url":
        prepare_agent_home_url(source, agent_home_dir)
    elif source_type == "github_artifact":
        prepare_agent_home_github_artifact(source, agent_home_dir)
    else:
        raise ValueError(f"Unknown agent_source.type: {source_type}")


def make_subprocess_env():
    """
    Build an environment for bash subprocesses that ensures PYTHON points to a
    usable Python interpreter.  The right value depends on which bash is being used:

    - Git Bash (MINGW, typical when invoking from Git Bash terminal): set PYTHON to
      the Windows Python path in forward-slash format (C:/Python/python.exe).  Git
      Bash can execute Windows binaries directly; subprocess-spawned non-interactive
      shells may not inherit the interactive PATH, so we pass the explicit path.

    - WSL2 bash (typical when invoking from PowerShell or cmd.exe): the Windows
      Python path is unusable inside WSL2.  Use 'python3' instead, which is
      available in standard WSL2 Ubuntu distributions.
    """
    env = os.environ.copy()

    if _bash_type_is_wsl2():
        env["PYTHON"] = "python3"
    else:
        env["PYTHON"] = sys.executable.replace("\\", "/")

    return env


def run_perf_test(run_cfg, test_cfg, label, agent_app_name, add_label_to_app_name):
    """Invoke run-perf-test.py for a single run configuration and return its exit code."""
    attach = run_cfg.get("attach_agent", False)

    if add_label_to_app_name:
        agent_app_name = f"{agent_app_name}-{label}"

    # Always use the relative path ./agent-home so Docker Compose resolves it
    # against its own project directory — the same way ./results and ./logs are
    # resolved.  Absolute Windows paths passed via AGENT_PATH env var bypass
    # Docker Compose's path resolution and reach the WSL2 daemon unparsed,
    # causing "invalid volume specification" errors.
    cmd = [
        sys.executable, "run-perf-test.py",
        "--attach-agent", "true" if attach else "false",
        "--agent-home", "./agent-home",
        "--app-name", agent_app_name,
        "--test-duration", str(test_cfg.get("duration", "2m")),
        "--locust-users", str(test_cfg.get("locust_users", 10)),
        "--locust-spawn-rate", str(test_cfg.get("locust_spawn_rate", 2)),
        "--dotnet-version", str(test_cfg.get("dotnet_version", "10.0")),
    ]

    license_key = os.environ.get("NEW_RELIC_LICENSE_KEY", "")
    collector_host = os.environ.get("NEW_RELIC_HOST", "")
    if license_key:
        cmd += ["--license-key", license_key]
    if collector_host:
        cmd += ["--collector-host", collector_host]

    agent_env = run_cfg.get("agent_env", {})
    for name, value in agent_env.items():
        cmd += ["--env", f"{name}={value}"]

    print(f"  Running: {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=SCRIPT_DIR, check=False)
    return result.returncode


def move_run_outputs(label, timestamp_dir):
    """Move results/ and logs/ from the script dir into a labeled subdirectory of timestamp_dir."""
    results_src = os.path.join(SCRIPT_DIR, "results")
    logs_src = os.path.join(SCRIPT_DIR, "logs")

    results_dst = os.path.join(timestamp_dir, f"perf-results-{label}")
    logs_dst = os.path.join(timestamp_dir, f"logs-{label}")

    for src, dst in [(results_src, results_dst), (logs_src, logs_dst)]:
        if os.path.isdir(src):
            os.makedirs(dst, exist_ok=True)
            for item in os.listdir(src):
                shutil.move(os.path.join(src, item), os.path.join(dst, item))


def docker_available():
    """Return True if Docker is available and responsive."""
    try:
        result = subprocess.run(
            ["docker", "info"],
            capture_output=True, timeout=10, check=False,
        )
        return result.returncode == 0
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return False


def generate_report(timestamp_dir):
    """Build the ReportGenerator Docker image and run it against timestamp_dir."""
    if not docker_available():
        print("WARNING: Docker is not available. Skipping report generation.")
        return

    print("\nBuilding ReportGenerator Docker image...")
    build_result = subprocess.run(
        ["docker", "build", "-t", "perf-report-generator", "ReportGenerator/"],
        cwd=SCRIPT_DIR, check=False,
    )
    if build_result.returncode != 0:
        print("ERROR: Failed to build ReportGenerator image.", file=sys.stderr)
        return

    report_dir = os.path.join(timestamp_dir, "report")
    os.makedirs(report_dir, exist_ok=True)

    print("Running ReportGenerator...")
    run_result = subprocess.run([
        "docker", "run", "--rm",
        "-v", f"{to_bash_path(timestamp_dir)}:/input:ro",
        "-v", f"{to_bash_path(report_dir)}:/output",
        "perf-report-generator",
        "--input-dir", "/input",
        "--output-dir", "/output",
    ], check=False)

    if run_result.returncode != 0:
        print("ERROR: ReportGenerator failed.", file=sys.stderr)
        return

    summary_path = os.path.join(report_dir, "summary.md")
    if os.path.exists(summary_path):
        print(f"\nReport generated: {summary_path}")
    else:
        print("WARNING: Report directory created but summary.md not found.")


def main():
    """Entry point: load config, run all comparison runs, and optionally generate a report."""
    args = parse_args()
    config = load_config(args.config)

    test_cfg = config.get("test", {})
    runs = config.get("runs", [])

    if not runs:
        print("ERROR: No runs defined in config.", file=sys.stderr)
        sys.exit(1)

    if args.results_dir:
        timestamp_dir = args.results_dir
    else:
        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        timestamp_dir = os.path.join(SCRIPT_DIR, "comparison-results", timestamp)

    os.makedirs(timestamp_dir, exist_ok=True)
    print(f"Results directory: {timestamp_dir}\n")

    failures = []

    for run_cfg in runs:
        label = run_cfg.get("label", "unnamed")
        print(f"{'=' * 60}")
        print(f"Run: {label}")
        print(f"{'=' * 60}")

        # agent-home/ lives next to docker-compose.yml so Docker Compose can
        # resolve it as a relative path, avoiding Windows absolute-path issues.
        agent_home_dir = os.path.join(SCRIPT_DIR, "agent-home")

        try:
            print("  Preparing agent home...")
            prepare_agent_home(run_cfg, agent_home_dir)
        except Exception as e:  # pylint: disable=broad-exception-caught
            print(f"  ERROR: Failed to prepare agent home for '{label}': {e}", file=sys.stderr)
            failures.append((label, str(e)))
            continue

        exit_code = run_perf_test(run_cfg, test_cfg, label, args.agent_app_name, args.add_label_to_app_name)

        move_run_outputs(label, timestamp_dir)

        if exit_code != 0:
            msg = f"run-perf-test.py exited with code {exit_code}"
            print(f"  FAIL: {msg}", file=sys.stderr)
            failures.append((label, msg))
        else:
            print(f"  PASS: Run '{label}' completed successfully.")

        print()

    print(f"{'=' * 60}")
    print("Summary")
    print(f"{'=' * 60}")
    passed = [r.get("label") for r in runs if r.get("label") not in {f[0] for f in failures}]
    for label in passed:
        print(f"  PASS  {label}")
    for label, reason in failures:
        print(f"  FAIL  {label}: {reason}")
    print()

    if not args.no_report:
        generate_report(timestamp_dir)
    else:
        print("Skipping report generation (--no-report).")


if __name__ == "__main__":
    main()
