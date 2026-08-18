#!/usr/bin/env bash
# Tests for check-payload-not-empty.sh: the guard must pass on a real total and
# fail on zero / missing bytes / missing file / below-floor.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$SCRIPT_DIR/../check-payload-not-empty.sh"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

fail() { echo "FAIL: $1" >&2; exit 1; }

# Non-zero total -> pass (exit 0).
echo '{"bytes": 69209939, "byType": {}}' > "$tmp/ok.json"
bash "$SCRIPT" "$tmp/ok.json" >/dev/null 2>&1 || fail "expected pass on non-zero total"

# Zero total -> fail (exit 1).
echo '{"bytes": 0, "byType": {}}' > "$tmp/zero.json"
if bash "$SCRIPT" "$tmp/zero.json" >/dev/null 2>&1; then fail "expected failure on zero total"; fi

# Missing bytes field (treated as 0) -> fail.
echo '{"byType": {}}' > "$tmp/missing.json"
if bash "$SCRIPT" "$tmp/missing.json" >/dev/null 2>&1; then fail "expected failure on missing bytes field"; fi

# Missing file -> fail.
if bash "$SCRIPT" "$tmp/does-not-exist.json" >/dev/null 2>&1; then fail "expected failure on missing file"; fi

# Below an explicit floor -> fail.
echo '{"bytes": 500}' > "$tmp/low.json"
if MIN_EXPECTED_PAYLOAD_BYTES=1000 bash "$SCRIPT" "$tmp/low.json" >/dev/null 2>&1; then fail "expected failure below floor"; fi

# Above an explicit floor -> pass.
echo '{"bytes": 5000}' > "$tmp/high.json"
MIN_EXPECTED_PAYLOAD_BYTES=1000 bash "$SCRIPT" "$tmp/high.json" >/dev/null 2>&1 || fail "expected pass above floor"

echo "PASS: all guard assertions passed"
