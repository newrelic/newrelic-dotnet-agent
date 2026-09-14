#!/usr/bin/env bash
# Preflight for the nr-agents marketplace. Reads only; writes nothing.
set -eu

HOST="source.datanerd.us"

PY=""
for candidate in python3 python; do
  if command -v "$candidate" >/dev/null 2>&1; then PY="$candidate"; break; fi
done
if [ -z "$PY" ]; then
  echo "Python 3 is required and was not found as python3 or python."
  echo "Install it from https://www.python.org/downloads/ and run this again."
  exit 1
fi
echo "python: $($PY --version 2>&1)"

if ! command -v git >/dev/null 2>&1; then
  echo "git is required and was not found."
  exit 1
fi
echo "git: $(git --version)"

if ! git config --get-all credential.helper >/dev/null 2>&1 \
   && ! git config --get "credential.https://${HOST}.helper" >/dev/null 2>&1; then
  echo "No git credential helper is configured for ${HOST}."
  echo "Set one up once, then run this again:"
  echo "  gh auth login --hostname ${HOST}"
  echo "  gh auth setup-git --hostname ${HOST}"
  exit 1
fi
echo "git credentials: a helper is configured for ${HOST}"

cat <<'ADVICE'

This machine is ready. Run these two lines in Claude Code:

  /plugin marketplace add https://source.datanerd.us/agents/claude-skills.git
  /plugin install dotnet-log-triage@nr-agents

Then set this in ~/.claude/settings.json under "env", so a failed background
refresh keeps your working copy instead of deleting it:

  "CLAUDE_CODE_PLUGIN_KEEP_MARKETPLACE_ON_FAILURE": "1"

To pull an update later, run /plugin marketplace update nr-agents by hand.

This script changed nothing.
ADVICE
