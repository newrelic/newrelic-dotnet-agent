---
name: update-dotty-docs
description: Use when the user mentions a Dotty PR, dotty (package/dependency) updates, or asks to update the .NET agent compatibility docs / net-agent-compatibility-requirements after tested library version bumps.
---

## Orchestration model

You orchestrate: find the PR, drive CI-failure decisions, review, create the
docs PR. Delegate context-heavy work to subagents so bulk output (diffs, ~90
CI-check lines, huge job logs, the docs `.mdx` search) stays out of your
context - you get only the slim result. Never run `gh pr diff`, `gh pr checks`,
`gh api .../jobs/<id>/logs`, or the docs `.mdx` search in the main thread.

Every subagent prompt MUST tell the subagent to:
- Run its `gh`/`git`/file commands directly.
- NOT invoke any skill and NOT spawn further subagents.
- Return ONLY the compact schema specified - not raw command output.

## Step 0: Prerequisites

Run `gh auth status`; if it fails, report the error and stop (covers gh-missing and not-authenticated). Git is assumed.

## Step 1: Find the open Dotty PR

Run yourself (you need the PR number in Step 8):

```
gh pr list --repo newrelic/newrelic-dotnet-agent --search "dotty" --state open --limit 1 --json number,title,body,url
```

If none found, stop. Otherwise record the PR number.

## Step 2 & 3: Intake diff + CI status (dispatch ONE subagent)

Dispatch one read-only subagent (Explore). Prompt (substitute the PR number):

> Run these two commands directly (do not invoke any skill; do not spawn
> subagents):
> - `gh pr diff <number> --repo newrelic/newrelic-dotnet-agent`
> - `gh pr checks <number> --repo newrelic/newrelic-dotnet-agent`
>
> From the diff, for each changed `<PackageReference>` where the Version
> changed, extract: package name (`Include`), old version, new version, and
> section from the `Condition`:
> - `net4xx` -> Framework
> - `net8.0`/`net10.0`/etc. -> Core
> - no Condition -> both
> Collapse identical package+old+new rows that differ only by TFM into one
> row with the combined section. Ignore `NewRelic.Agent.Api` bumps.
>
> From the checks, look only at jobs matching `Run IntegrationTests (*)`,
> `Run Unbounded Tests (*)`, and `Run Linux Container Tests (*)`. Report the
> overall verdict.
>
> Return ONLY this, nothing else:
> 1. A markdown table: `package | old | new | section`.
> 2. CI: either the literal `all passing`, or a table of failing jobs:
>    `name | job_id | url` (extract job_id from the job URL's trailing number).

If CI is `all passing`, skip to Step 4. Otherwise, for each failing job use
`AskUserQuestion` with three options:
- **Analyze** - see Step 3a (dispatch a log-analysis subagent).
- **Skip** - exclude any package that job could implicate from the docs update; continue.
- **Stop** - end the skill.

After handling all failing jobs, summarize which packages are safe to update.

## Step 3a: Analyze a failing job (dispatch ONE subagent per job, in parallel)

On Analyze, dispatch one read-only subagent (Explore) per failing job, all in
a single message so they run concurrently. Prompt each (substitute job_id and
paste the package-change table):

> Run this directly (do not invoke any skill; do not spawn subagents):
> `gh api repos/newrelic/newrelic-dotnet-agent/actions/jobs/<job_id>/logs`
>
> The following packages were bumped in this PR:
> <package-change table>
>
> Correlate the failing errors in the log with these packages. Check for
> transitive dependency conflicts (example: `OpenAI` 2.9.0 removed a type
> that `Azure.AI.OpenAI` depends on - inspect dependency chains in
> `tests/Agent/IntegrationTests/SharedApplications/Common/MFALatestPackages/MFALatestPackages.csproj`
> in the current repo if needed).
>
> Return ONLY: a short list of implicated package names, each with a
> one-line reason. If nothing is implicated, return `no packages implicated`.

Aggregate the returned lists. Present implicated packages to the user and
confirm which are safe to update before continuing.

## Step 4: Flag major version bumps

From the slim table (Step 2 & 3), for any major version change (e.g., 3.x -> 4.x), report it prominently - it may block merging the Dotty PR.

## Step 5: Prepare the docs repo

1. Capture the user's GitHub login: `gh api user --jq '.login'` -> `$GH_USER`. Verify fork exists: `gh api repos/$GH_USER/docs-website --jq '.full_name'`. If no fork, stop (do not create one).
2. Clone to a known path (not a bash tempdir - the path must survive across tool calls). Use `~/tmp/docs-website-dotty-<date>` or similar; **record the absolute path** for later steps.
   ```
   git clone --branch develop https://github.com/$GH_USER/docs-website.git <path>
   cd <path>
   git remote add upstream https://github.com/newrelic/docs-website.git
   git fetch upstream develop
   git reset --hard upstream/develop
   git push origin develop
   git checkout -b dotnet/dotty-updates-YYYY-MM-DD
   ```
   (`reset --hard` is safe - the fork's `develop` has no local commits to preserve.)

All subsequent steps run inside the clone path.

## Step 6: Update the docs file (dispatch ONE subagent)

Dispatch one edit-capable subagent (general-purpose). Prompt (substitute the
clone path and paste the safe-to-update package table):

> Work inside `<clone-path>`. Do not invoke any skill; do not spawn subagents.
> Edit this file only:
> `src/content/docs/apm/agents/net-agent/getting-started/net-agent-compatibility-requirements.mdx`
>
> It has two tabbed sections: `.NET Core` (`id="core"`) and `.NET Framework`
> (`id="framework"`). For each package below, update the version in the
> section(s) matching its `section` column (both = update both tabs).
>
> Packages to update:
> <safe-to-update package table>
>
> Package-to-docs mapping: not every package has a docs entry - skip silently
> if the name is not found. Always search the file for the package name.
> Non-obvious mappings: `Microsoft.Azure.Cosmos` -> Cosmos DB,
> `CouchbaseNetClient` -> Couchbase, `EnyimMemcachedCore` -> Memcached,
> `AWSSDK.BedrockRuntime` -> AWS Bedrock,
> `Elastic.Clients.Elasticsearch` -> Elasticsearch, `MySql.Data` -> MySQL,
> `Npgsql` -> PostgreSQL, `AWSSDK.DynamoDBv2` -> DynamoDB. Logging/LLM/AWS-SDK
> packages typically map by exact name.
>
> Version formats - change only the version number, nothing else:
> - Bullet (Datastores, Messaging): `* Latest verified compatible version: 3.57.0`
> - Table column (LLM, Logging - last `<td>` in row): the version inside the `<td>`.
>
> Return ONLY: a table `package | old | new | section | updated?` where
> `updated?` is `yes` or `no docs entry`.

## Step 7: Present for review

Present, from inside the clone path:
1. The Step 6 summary table (package, old, new, section, updated?).
2. Major version bumps from Step 4.
3. The full diff - run `git diff` yourself (small, controlled change).

**Do not commit or push without explicit approval.**

## Step 8: After approval - commit, push, PR, comment

**Never use "Dotty" (team-specific term) in the commit message or PR title/body;
use "latest supported library versions" phrasing. Branch name may keep `dotty`.**

1. Commit: `chore(.net agent): Update compatibility docs for latest supported library versions`
2. Push the branch to origin.
3. Create PR against upstream:
   ```
   gh pr create --repo newrelic/docs-website --base develop --head $GH_USER:<branch> \
     --title "chore(.net agent): Update compatibility docs for latest supported library versions" \
     --body "<brief list of packages updated>"
   ```
4. Comment on the Dotty PR:
   ```
   gh pr comment <dotty-pr-number> --repo newrelic/newrelic-dotnet-agent \
     --body "Compatibility docs update PR created: <docs-pr-url>"
   ```
5. Remove the clone directory (use the absolute path recorded in Step 5).
