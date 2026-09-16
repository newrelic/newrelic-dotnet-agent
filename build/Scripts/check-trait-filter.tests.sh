#!/usr/bin/env bash
# Tests for check-trait-filter.sh. Run: bash build/Scripts/check-trait-filter.tests.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
SUT="$HERE/check-trait-filter.sh"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
fails=0

expect() { # expect <label> <wanted-exit> <exe> [filter]
  local label="$1" want="$2" exe="$3" filter="${4:-Platform=WindowsOnly}" got
  bash "$SUT" "$filter" "$exe" >"$TMP/out" 2>&1
  got=$?
  if [ "$got" -ne "$want" ]; then
    echo "FAIL $label: wanted exit $want, got $got"
    sed 's/^/    /' "$TMP/out"
    fails=$((fails + 1))
  else
    echo "PASS $label"
  fi
}

# Writes a stub assembly. The heredoc delimiter is unquoted on purpose: $2 and $3
# are this function's arguments and get baked in, while \$3 stays literal and is
# the flag the stub receives at run time. Each case prints a banner, a blank line,
# then class names, exactly as the xunit v3 runner does.
stub() { # stub <name> <matched-lines> <remaining-lines>
  cat > "$TMP/$1" <<STUB
#!/usr/bin/env bash
echo "xUnit.net v3 In-Process Runner v4.0.0 (stub)"
echo ""
if [ "\$3" = "-trait" ]; then printf '%s\n' $2; else printf '%s\n' $3; fi
STUB
  chmod +x "$TMP/$1"
}

stub healthy   "Ns.WinA Ns.WinB" "Ns.PortA Ns.PortB"
stub dead      "Ns.WinA Ns.WinB" "Ns.WinA Ns.WinB Ns.PortA"
stub nomatch   ""                "Ns.PortA Ns.PortB"
stub allwin    "Ns.WinA Ns.WinB" ""

expect "disjoint sets pass"                 0 "$TMP/healthy"
expect "exclusion that excludes nothing"    1 "$TMP/dead"
expect "trait absent from assembly"         1 "$TMP/nomatch"
expect "every class carries the trait"      1 "$TMP/allwin"
expect "missing executable"                 1 "$TMP/absent"

# Writes a stub that crashes instead of listing classes, to prove a launch
# failure is reported as a launch failure and never mistaken for a missing
# trait -- the defect this script fixes.
cat > "$TMP/crashes" <<'STUB'
#!/usr/bin/env bash
echo "Unhandled exception: could not load file or assembly."
exit 1
STUB
chmod +x "$TMP/crashes"

bash "$SUT" "Platform=WindowsOnly" "$TMP/crashes" >"$TMP/out" 2>&1
got=$?
if [ "$got" -eq 1 ] && grep -q "Test launcher exited" "$TMP/out" && ! grep -q "No class matches" "$TMP/out"; then
  echo "PASS launcher exits non-zero is reported as a launch failure, not a missing trait"
else
  echo "FAIL launcher exits non-zero is reported as a launch failure, not a missing trait: exit=$got"
  sed 's/^/    /' "$TMP/out"
  fails=$((fails + 1))
fi

echo "----"
[ "$fails" -eq 0 ] && { echo "all checks passed"; exit 0; }
echo "$fails check(s) failed"; exit 1
