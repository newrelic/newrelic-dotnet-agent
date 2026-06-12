---
name: angler-dotnet-release
description: Open an Angler PR that adds the latest .NET agent release to metric_names.txt. Use when the user asks to add a .NET agent version to Angler, update Angler for a new agent release, or "create the Angler PR" after a .NET agent release ships.
disable-model-invocation: true
---

# Add latest .NET agent release to Angler

When a new .NET agent version ships, Angler needs a one-line addition to its
`metric_names.txt` so the new `Supportability/AgentVersion/<version>` metric is
recognized. This skill discovers the latest release, makes that edit, opens the
PR end-to-end via the GitHub API (no local clone of Angler is needed), and -- for
a ready-for-review PR -- posts an approval request to the #dotnet-team Slack
channel if a Slack tool is connected.

Reference for the shape of the change: Angler PR #837
(`https://source.datanerd.us/agents/angler/pull/837`) -- a single line added to
`src/main/resources/metric_names.txt`.

The bundled script `scripts/create_angler_pr.sh` does the work. It needs only
`gh` plus standard shell tools (`awk`, `base64`, `grep`) that ship with the same
Git Bash that runs `gh` -- no extra runtime to install. It manipulates the
~7,600-line file inside the shell, so that content never enters context.

## What the change looks like

The latest release tag (e.g. `v10.51.1`) becomes a 4-part version `10.51.1.0`
(a 3-part tag gets a trailing `.0`). That line is inserted at the **top** of the
`// .NET agent version metrics` section, which is ordered newest-first:

```
// .NET agent version metrics
Supportability/AgentVersion/10.51.1.0   <- inserted
Supportability/AgentVersion/10.51.0.0
```

Functionally, placement does not matter -- Angler's `MetricLoader` strips
comments/blank lines and hashes each remaining line independently, so the file
is an unordered set. The newest-first slotting is purely for human tidiness.

The PR targets `master` and is titled `Add .NET Agent v<version> to Angler`.
It is opened **ready for review** by default, or as a **draft** when the release
also adds supportability metrics that must be added by hand (see Step 2).

## Step 0: Prerequisites

Run `gh auth status`. It must report authentication for **both** `github.com`
(to read the latest release) and `source.datanerd.us` (to open the Angler PR).
If either is missing, stop and tell the user which host needs auth.

## Step 1: Check what would change (dry run)

```bash
bash .claude/skills/angler-dotnet-release/scripts/create_angler_pr.sh --dry-run
```

This prints the detected version, whether its line is already present, and the
diff -- without creating anything. If it reports `status: PRESENT`, Angler is
already up to date for this version; stop and report that (a normal outcome, not
an error). Note: if the version line is already present but the user has extra
supportability metrics to add, this skill makes no PR -- those need a separate
manual edit.

To target a specific version instead of the latest release, add `--version X.Y.Z`.

## Step 2: Ask about supportability metrics

Some .NET agent releases also add new `Supportability/...` metrics that Angler
needs alongside the version line. The agent team knows when this happened; the
skill cannot reliably detect it (the names are partly composed at runtime). So
ask the user before opening the PR:

> Does this release also add new supportability metrics that need to go into
> Angler, beyond the version line?

- **No** -> open a normal, ready-for-review PR (Step 3 without `--draft`).
- **Yes** -> open a **draft** PR so the extra metrics can be added by hand
  first (Step 3 with `--draft`). The script prints a web-edit URL to the file on
  the branch; relay it so the user can add the metrics and mark the PR ready.

## Step 3: Open the PR

```bash
# ready for review (no extra metrics)
bash .claude/skills/angler-dotnet-release/scripts/create_angler_pr.sh

# draft (extra supportability metrics to be added by hand)
bash .claude/skills/angler-dotnet-release/scripts/create_angler_pr.sh --draft
```

Pass the same `--version X.Y.Z` here if one was used in Step 1.

## Step 4: Notify #dotnet-team on Slack

A ready-for-review PR needs a teammate's approval, so let the team know. Do this
only when the PR was opened **ready for review** -- a draft is not ready for
approval yet, so skip this for a draft and notify once the PR is marked ready.

First check whether a Slack send-message tool is available in this session
(e.g. `slack_send_message`). The plugin that provides it may not be connected --
that is fine and **not a failure**.

- **Slack tool available** -> post to the private channel **#dotnet-team**.
  Resolve its id by searching channels for `dotnet-team` with private channels
  included, and pick the exact-name match (the public `#dotnet-agent` also
  matches that search -- do not post there). Send a message like:

  > An Angler PR for .NET Agent v<version> has been created and needs
  > review/approval: <PR URL>

  Do not use `@here` / `@channel` mentions unless the user asks for them. Then
  report the link to the message you posted.

- **Slack tool not available** -> not a failure. Tell the user to post to
  #dotnet-team asking for approval, and give them ready-to-paste text:

  > An Angler PR for .NET Agent v<version> has been created and needs
  > review/approval: <PR URL>

## Step 5: Report the result

Relay what the script printed: the PR URL, and for a draft, the web-edit URL
plus a reminder to add the extra metrics and mark the PR ready when done. If you
posted to Slack in Step 4, include the message link; if the Slack tool was
unavailable, include the suggested text for the user to post manually.

If the script fails, surface the message verbatim. Common causes: not
authenticated to one of the two hosts, or a branch/PR from a prior attempt
already exists (the script reuses an existing branch, but the GitHub API rejects
a duplicate open PR for the same head) -- point the user at the existing
branch/PR rather than retrying blindly.
