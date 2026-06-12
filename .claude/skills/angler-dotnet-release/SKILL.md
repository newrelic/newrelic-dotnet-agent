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

If it reports `status: PRESENT`, Angler is already up to date; stop and report
that (a normal outcome, not an error).

To target a specific version instead of the latest release, add `--version X.Y.Z`.

## Step 2: Discover new supportability metrics

The discovery tool reflects over the compiled agent, so `FullAgent.sln` must
be built before running it. Ask the user for permission before building:

Check whether `src/Agent/newrelichome_x64_coreclr/NewRelic.Agent.Core.dll`
exists:

- **File is absent:** tell the user the agent has not been built and ask:
  > `FullAgent.sln` has not been built yet. Build it now so the discovery
  > tool can run? This may take a few minutes.
- **File is present:** ask:
  > `FullAgent.sln` was built previously. Rebuild now to make sure the
  > discovery tool reflects the latest agent code? (Skip if you know the
  > build is already current.)

**If the user says yes (or the default):** run the build, then proceed:

```bash
dotnet build FullAgent.sln
```

**If the user says no and the DLL is absent:** skip discovery entirely and
proceed to Step 3, opening the PR ready-for-review.

**If the user says no and the DLL is present:** proceed with the existing
build -- the discovery results may not reflect uncommitted changes.

Once the build is confirmed, fetch the Angler file and run the tool:

```bash
# Fetch Angler file if not already on disk from Step 1
gh api --hostname source.datanerd.us \
  "repos/agents/angler/contents/src/main/resources/metric_names.txt?ref=master" \
  --jq .content | base64 -d > /tmp/angler_current.txt

dotnet run --project build/MetricNameDiscovery/MetricNameDiscovery.csproj \
  -- --diff /tmp/angler_current.txt 2>/dev/null
```

Parse every line from the `=== Candidate additions ===` section (lines starting
with `  + `). Strip the leading `  + ` to get the bare metric name.

**If no candidates are found:** proceed to Step 3 and open ready-for-review.

**If candidates are found:** present the numbered list to the user and ask them
to categorize each one:

> The discovery tool found the following .NET supportability metrics that are
> in the agent code but not yet in Angler:
>
> 1. <metric name>
> 2. <metric name>
> ...
>
> For each metric, reply with one of:
> - **A** -- add to Angler (include in this PR)
> - **E** -- add to the exclusions list (creates a separate dotnet-agent PR)
> - **S** -- skip for now (take no action)
>
> Example: "1=A, 2=E, 3=S"

### If any metrics are classified E (add to exclusions)

Before opening the Angler PR, commit the exclusions to the dotnet-agent repo:

1. Edit `build/MetricNameDiscovery/exclusions.txt` -- append each metric with
   a `#` comment (ask the user for the reason if they did not provide one, or
   use "triaged <date>" as a default).
2. Create a branch (e.g. `chore/metric-discovery-exclusions-<version>`), commit
   the file, and open a **draft** PR in the dotnet-agent repo:
   - Title: `chore: Add metric discovery exclusions for v<version>`
   - Body: list the excluded names
3. Report the exclusions PR URL to the user before continuing.

Then proceed to Step 3 for the Angler PR.

## Step 3: Open the PR

Determine the flags:
- Add one `--extra-metric "NAME"` for each metric the user classified **A** in
  Step 2. Omit the flag entirely if none were classified A. Extra metrics are
  inserted under the `// .NET MISC` section (not alongside the version line).
- Add `--draft` only if there are no A metrics AND the user explicitly asked for
  a draft. If any A metrics exist, the PR can be ready-for-review (the script
  commits everything in one shot).

```bash
# No extra metrics -- ready for review
bash .claude/skills/angler-dotnet-release/scripts/create_angler_pr.sh

# With extra metrics -- still ready for review (all lines committed by the script)
bash .claude/skills/angler-dotnet-release/scripts/create_angler_pr.sh \
  --extra-metric "Supportability/Dotnet/NetFramework/net481" \
  --extra-metric "Supportability/DotNET/AppDomainCaching/Disabled"
```

Pass the same `--version X.Y.Z` here if one was used in Step 1.

## Step 4: Notify #dotnet-team on Slack

A ready-for-review PR needs a teammate's approval, so let the team know. Do this
only when the PR was opened **ready for review** -- a draft is not ready for
approval yet, so skip this for a draft and notify once the PR is marked ready.

First check whether a Slack send-message tool is available in this session
(e.g. `slack_send_message`). The plugin that provides it may not be connected --
that is fine and **not a failure**.

In both cases the message is:

> An Angler PR for .NET Agent v<version> has been created and needs
> review/approval: <PR URL>

- **Slack tool available** -> post to the private channel **#dotnet-team**.
  Resolve its id by searching channels for `dotnet-team` with private channels
  included, and pick the exact-name match (the public `#dotnet-agent` also
  matches that search -- do not post there). Do not use `@here` / `@channel`
  unless the user asks. Report the link to the message you posted.

- **Slack tool not available** -> not a failure. Give the user the message
  text above to post manually.

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
