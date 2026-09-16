#!/usr/bin/env bash
# Proves an exclusion trait filter is live before a lane runs. check-test-run.sh
# cannot see a dead exclusion filter: a broken one excludes nothing, so the lane
# runs everything and still passes on a Windows runner. Controls come from the
# assembly itself, so there are no class names or counts to keep in sync.
# Usage: check-trait-filter.sh <test-executable> <name=value>
set -uo pipefail

exe="${1:?test executable required}"
filter="${2:?trait filter as name=value required}"

if [ ! -f "$exe" ]; then
  echo "::error::No test executable at $exe."
  exit 1
fi

classes() { # classes <-trait|-trait->
  "$exe" -list classes "$1" "$filter" 2>/dev/null \
    | grep -E '^[A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z0-9_]+$'
}

matched="$(classes -trait | sort)"
remaining="$(classes -trait- | sort)"

if [ -z "$matched" ]; then
  echo "::error::No class matches $filter. Either the trait is absent from this assembly (a stale binary, or the trait tool was never run) or the filter is misspelled. An exclusion lane cannot be trusted without it."
  exit 1
fi

if [ -z "$remaining" ]; then
  echo "::error::Excluding $filter leaves no class at all. Every class carries the trait, which is not a lane."
  exit 1
fi

leaked="$(comm -12 <(echo "$matched") <(echo "$remaining"))"
if [ -n "$leaked" ]; then
  echo "::error::-trait- $filter failed to exclude $(echo "$leaked" | wc -l) class(es) that -trait $filter matched. The exclusion filter is dead and this lane would run Windows-only tests."
  echo "$leaked" | head -5 | sed 's/^/    /'
  exit 1
fi

echo "Exclusion filter live: $(echo "$matched" | wc -l) class(es) match $filter, all excluded; $(echo "$remaining" | wc -l) remain."
exit 0
