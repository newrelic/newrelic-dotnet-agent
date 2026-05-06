---
name: update-dotty-docs
description: Update .NET agent compatibility docs based on a Dotty PR. Use when the user mentions a Dotty PR, dotty updates, or updating compatibility docs.
disable-model-invocation: true
---

## Step 0: Prerequisites

Run `gh auth status`. If it fails, stop and report the error to the user. (This also covers "gh not installed" and "not authenticated." Git is assumed.)

## Step 1: Find the open Dotty PR

```
gh pr list --repo newrelic/newrelic-dotnet-agent --search "dotty" --state open --limit 1 --json number,title,body,url
```

If none found, stop.

## Step 2 & 3: Fetch diff and CI status (run in parallel)

```
gh pr diff <number> --repo newrelic/newrelic-dotnet-agent
gh pr checks <number> --repo newrelic/newrelic-dotnet-agent
```

**Parse the diff.** For each `<PackageReference>` change, extract: package name (`Include`), old/new versions, and section from `Condition`:
- `net4xx` → .NET Framework
- `net8.0`/`net10.0`/etc. → .NET Core
- No condition → both

**Check CI.** Focus on `Run IntegrationTests (*)`, `Run UnboundedIntegrationTests (*)`, `Run Linux Container Tests (*)`.

If any fail, report them with links, then for each failing job use `AskUserQuestion` with three options:
- **Analyze** — fetch logs (`gh api repos/newrelic/newrelic-dotnet-agent/actions/jobs/<job_id>/logs`), correlate errors with updated packages, check for transitive dependency conflicts (e.g., `OpenAI` 2.9.0 removed a type `Azure.AI.OpenAI` depends on — compare dependency chains in `MFALatestPackages.csproj`). Report which packages are implicated.
- **Skip** — exclude potentially-related packages from the docs update; continue.
- **Stop** — end the skill.

After the loop, summarize which packages are safe to update.

## Step 4: Flag major version bumps

For any major version change (e.g., 3.x → 4.x), report prominently — may block merging the Dotty PR.

## Step 5: Prepare the docs repo

1. Capture the user's GitHub login: `gh api user --jq '.login'` → `$GH_USER`. Verify fork exists: `gh api repos/$GH_USER/docs-website --jq '.full_name'`. If no fork, stop (do not create one).
2. Clone to a known path (not a bash tempdir — the path must survive across tool calls). Use `~/tmp/docs-website-dotty-<date>` or similar; **record the absolute path** for later steps.
   ```
   git clone --branch develop https://github.com/$GH_USER/docs-website.git <path>
   cd <path>
   git remote add upstream https://github.com/newrelic/docs-website.git
   git fetch upstream develop
   git reset --hard upstream/develop
   git push origin develop
   git checkout -b dotnet/dotty-updates-YYYY-MM-DD
   ```
   (`reset --hard` is safe — the fork's `develop` has no local commits to preserve.)

All subsequent steps run inside the clone path.

## Step 6: Update the docs file

File: `src/content/docs/apm/agents/net-agent/getting-started/net-agent-compatibility-requirements.mdx`

Two tabbed sections: `.NET Core` (`id="core"`) and `.NET Framework` (`id="framework"`). Update the section(s) matching each package's target framework from Step 2. Only update packages whose CI is passing.

**Package-to-docs mapping.** Not every Dotty package has a docs entry — skip silently if no match. Always search the docs file for the package name. Non-obvious mappings: `Microsoft.Azure.Cosmos` → Cosmos DB, `CouchbaseNetClient` → Couchbase, `EnyimMemcachedCore` → Memcached, `AWSSDK.BedrockRuntime` → AWS Bedrock, `Elastic.Clients.Elasticsearch` → Elasticsearch, `MySql.Data` → MySQL, `Npgsql` → PostgreSQL, `AWSSDK.DynamoDBv2` → DynamoDB. Logging/LLM/AWS-SDK packages typically map by exact name.

**Version formats.** Update only the version number, nothing else.
- Bullet (Datastores, Messaging): `* Latest verified compatible version: 3.57.0`
- Table column (LLM, Logging — last `<td>` in row): `<td>\n    2.8.0\n</td>`

## Step 7: Present for review

Show: (1) summary table of updates (package, old, new, section), (2) major version bumps from Step 4, (3) the full diff. **Do not commit or push without explicit approval.**

## Step 8: After approval — commit, push, PR, comment

1. Commit: `chore(.net agent): Update compatibility docs for latest Dotty package versions`
2. Push the branch to origin.
3. Create PR against upstream:
   ```
   gh pr create --repo newrelic/docs-website --base develop --head $GH_USER:<branch> \
     --title "chore(.net agent): Updating latest supported framework versions" \
     --body "<brief list of packages updated>"
   ```
4. Comment on the Dotty PR:
   ```
   gh pr comment <dotty-pr-number> --repo newrelic/newrelic-dotnet-agent \
     --body "Compatibility docs update PR created: <docs-pr-url>"
   ```
5. Remove the clone directory (use the absolute path recorded in Step 5).
