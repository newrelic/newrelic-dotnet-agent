#!/usr/bin/env bash
# Create an Angler PR adding the latest .NET agent release to metric_names.txt.
#
# The change is a single line inserted at the top of the
# "// .NET agent version metrics" section of
# src/main/resources/metric_names.txt in agents/angler (source.datanerd.us):
#
#     Supportability/AgentVersion/<X.Y.Z.0>
#
# Everything runs through the GitHub API via the `gh` CLI -- no local clone of
# angler is needed. github.com is used to discover the latest dotnet-agent
# release; source.datanerd.us hosts angler. Only gh plus standard shell tools
# (awk, base64, grep) are required -- all ship with the Git Bash that runs gh.
#
# Usage:
#   create_angler_pr.sh [--version X.Y.Z[.W]] [--draft] [--dry-run]
#
#   --version  Target a specific agent version instead of the latest release.
#              A 3-part version gets a trailing ".0" (matching the file format).
#   --draft    Open the PR as a draft and print a web-edit URL for the file on
#              the branch. Use when the release also adds supportability metrics
#              that must be added by hand before the PR is ready.
#   --dry-run  Print the detected version and the diff without creating anything.

set -e

HOST="source.datanerd.us"
REPO="agents/angler"
BASE="master"
FILE="src/main/resources/metric_names.txt"
AGENT_REPO="newrelic/newrelic-dotnet-agent"

version=""
draft="false"
dryrun="false"

while [ $# -gt 0 ]; do
  case "$1" in
    --version) version="$2"; shift 2 ;;
    --version=*) version="${1#*=}"; shift ;;
    --draft) draft="true"; shift ;;
    --dry-run) dryrun="true"; shift ;;
    -h|--help) grep '^# ' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

# Resolve the target version to its 4-part form (vX.Y.Z / X.Y.Z -> X.Y.Z.0).
if [ -z "$version" ]; then
  version=$(gh api "repos/$AGENT_REPO/releases/latest" --jq .tag_name)
fi
version="${version#v}"; version="${version#V}"
nparts=$(echo "$version" | awk -F. '{print NF}')
if [ "$nparts" -eq 3 ]; then
  version="$version.0"
elif [ "$nparts" -ne 4 ]; then
  echo "ERROR: expected a 3- or 4-part version, got '$version'" >&2
  exit 1
fi

line="Supportability/AgentVersion/$version"
branch="add-dotnet-agent-v$version"
title="Add .NET Agent v$version to Angler"

echo "version:     $version"
echo "metric line: $line"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Fetch the current file content and its blob sha from the base branch. The
# content is written to disk (not held in a pipe) so a downstream early-exit
# can't trigger a SIGPIPE warning from gh, and so the large blob stays out of
# any calling context.
sha=$(gh api --hostname "$HOST" "repos/$REPO/contents/$FILE?ref=$BASE" --jq .sha)
gh api --hostname "$HOST" "repos/$REPO/contents/$FILE?ref=$BASE" --jq .content \
  | base64 -d > "$work/metrics.txt"

# Idempotency: if the line already exists, there is nothing to do.
if grep -qxF "$line" "$work/metrics.txt"; then
  echo "status: PRESENT -- already in $FILE, no PR needed."
  exit 0
fi
echo "status: ABSENT"

# Insert the new line immediately after the section anchor (newest-first order).
awk -v ins="$line" '
  {print}
  /^\/\/ \.NET agent version metrics[[:space:]]*$/ && !done {print ins; done=1}
' "$work/metrics.txt" > "$work/metrics_new.txt"

# Guard against a missing/renamed anchor producing an unchanged file.
if ! grep -qxF "$line" "$work/metrics_new.txt"; then
  echo "ERROR: anchor '// .NET agent version metrics' not found; file unchanged." >&2
  exit 1
fi

echo
echo "diff:"
echo "  // .NET agent version metrics"
echo "+ $line"

if [ "$dryrun" = "true" ]; then
  kind="PR"; [ "$draft" = "true" ] && kind="draft PR"
  echo
  echo "[dry-run] would create branch '$branch' and open $kind: '$title'"
  exit 0
fi

# Create the branch from the current base head.
basesha=$(gh api --hostname "$HOST" "repos/$REPO/git/ref/heads/$BASE" --jq .object.sha)
if err=$(gh api --hostname "$HOST" "repos/$REPO/git/refs" \
      -f ref="refs/heads/$branch" -f sha="$basesha" 2>&1); then
  echo "created branch $branch"
elif echo "$err" | grep -q "Reference already exists"; then
  echo "branch $branch already exists -- reusing it."
else
  echo "$err" >&2
  exit 1
fi

# Commit the edited file onto the branch. base64 contains no JSON-special
# characters, so the request body can be built with printf (no jq needed) and
# sent via --input to avoid command-line length limits on the large content.
content=$(base64 -w0 "$work/metrics_new.txt")
printf '{"message":"%s","content":"%s","sha":"%s","branch":"%s"}' \
  "$title" "$content" "$sha" "$branch" > "$work/put.json"
gh api --hostname "$HOST" -X PUT "repos/$REPO/contents/$FILE" \
  --input "$work/put.json" >/dev/null
echo "committed change to $FILE"

# Open the PR -- ready for review by default, draft when metrics are pending.
if [ "$draft" = "true" ]; then
  body="Draft: additional .NET agent supportability metrics still need to be added to metric_names.txt before this is ready for review."
  printf '{"title":"%s","head":"%s","base":"%s","body":"%s","draft":true}' \
    "$title" "$branch" "$BASE" "$body" > "$work/pr.json"
else
  printf '{"title":"%s","head":"%s","base":"%s","body":""}' \
    "$title" "$branch" "$BASE" > "$work/pr.json"
fi
url=$(gh api --hostname "$HOST" "repos/$REPO/pulls" --input "$work/pr.json" --jq .html_url)

if [ "$draft" = "true" ]; then
  echo "draft PR opened: $url"
  echo "add the supportability metrics here, then mark the PR ready:"
  echo "  https://$HOST/$REPO/edit/$branch/$FILE"
else
  echo "PR opened: $url"
fi
