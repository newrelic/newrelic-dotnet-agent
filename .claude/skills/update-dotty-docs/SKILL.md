---
name: update-dotty-docs
description: Update .NET agent compatibility docs based on a Dotty PR. Use when the user mentions a Dotty PR, dotty updates, or updating compatibility docs.
disable-model-invocation: true
---

# Update .NET Agent Compatibility Docs from Dotty PR

Update the "compatibility and requirements" documentation in the docs-website repo based on package updates from the current open Dotty PR.

## Step 1: Find the open Dotty PR

Search for the open Dotty PR:
```
gh pr list --repo newrelic/newrelic-dotnet-agent --search "dotty" --state open --limit 1 --json number,title,body,url
```

If no open Dotty PR is found, inform the user and stop.

## Step 2: Parse the package updates

Fetch the PR diff:
```
gh pr diff <number> --repo newrelic/newrelic-dotnet-agent
```

From each `<PackageReference>` change, extract:
- **Package name** (`Include` attribute)
- **Old version** and **new version**
- **Target framework** from `Condition` attribute:
  - `net4xx` (e.g., `net481`) = .NET Framework section
  - `net8.0`, `net10.0`, etc. = .NET Core section
  - No condition = both sections

## Step 3: Check CI status

Check the all_solutions workflow status for the Dotty PR:
```
gh pr checks <number> --repo newrelic/newrelic-dotnet-agent
```

Look for any failing jobs, especially in:
- **Integration test** jobs (`Run IntegrationTests (*)`)
- **Unbounded integration test** jobs (`Run UnboundedIntegrationTests (*)`)
- **Container test** jobs (`Run Linux Container Tests (*)`)

If there are **any failing CI jobs**, report them prominently:

> **CI FAILURES:** The following jobs are failing on the Dotty PR:
> - {job name} — [link]
>
> An all-passing CI run is required to confirm the updated libraries don't break tests.

Then offer to diagnose the failures:

> Would you like me to analyze the test failures to determine if they're caused by the Dotty package updates?

**If the user declines:** Ask whether to proceed with docs updates (excluding packages related to the failing tests) or wait for a green build.

**If the user accepts:** Perform a detailed diagnosis:

1. Fetch the failing job's logs:
   ```
   gh api repos/newrelic/newrelic-dotnet-agent/actions/jobs/<job_id>/logs
   ```
2. Search the logs for error messages, exceptions, and stack traces.
3. Correlate failures with specific package updates from Step 2 — check whether the error references types, assemblies, or versions from updated packages.
4. Check for **transitive dependency conflicts** — a common pattern is Package A being updated to a version that's incompatible with Package B (e.g., `OpenAI` 2.9.0 removing a type that `Azure.AI.OpenAI` depends on). Compare the dependency chains of updated packages against their co-dependencies in `MFALatestPackages.csproj`.
5. Present findings:
   - Which test(s) failed and the specific error
   - Whether the failure is caused by a Dotty package update (and which one)
   - Recommended remediation (e.g., revert the specific package, pin a version, or wait for an upstream fix)
   - Which docs updates are safe to proceed with and which should be held

## Step 4: Flag major version bumps

If any package has a **major version change** (e.g., 3.x to 4.x), report it prominently BEFORE proceeding:

> **MAJOR VERSION BUMP:** {Package} {old} -> {new}
> Major version updates require deeper compatibility evaluation and may block merging the Dotty PR.

## Step 5: Prepare the docs repo

The docs repo is a fork at `C:\Source\repos\docs-website`.

1. Ensure an `upstream` remote exists pointing to `newrelic/docs-website`. Add it if missing.
2. Fetch upstream and sync the `develop` branch:
   ```
   git fetch upstream
   git checkout develop
   git merge upstream/develop
   git push origin develop
   ```
3. Create a feature branch from `develop` (e.g., `dotnet/dotty-updates-YYYY-MM-DD`).

## Step 6: Update the docs file

The file to edit is:
```
src/content/docs/apm/agents/net-agent/getting-started/net-agent-compatibility-requirements.mdx
```

This MDX file has two tabbed sections: `.NET Core` (`id="core"`) and `.NET Framework` (`id="framework"`). Update the correct section based on the target framework from Step 2. Only update entries for packages where CI tests are passing.

### Package-to-docs mapping

Not every Dotty package has a docs entry. Some are supporting dependencies (e.g., `AWSSDK.SecurityToken`) that aren't directly instrumented. Skip packages that don't have a matching entry — do not mention skipped packages in the output.

Search the docs file to find matching entries. Common mappings include:

| NuGet Package | Docs Entry |
|---|---|
| `Microsoft.Azure.Cosmos` | Cosmos DB |
| `CouchbaseNetClient` | Couchbase |
| `StackExchange.Redis` | StackExchange.Redis |
| `EnyimMemcachedCore` | Memcached |
| `AWSSDK.DynamoDBv2` | DynamoDB |
| `Confluent.Kafka` | Confluent.Kafka |
| `AWSSDK.SQS` | AWSSDK.SQS |
| `AWSSDK.Kinesis` | AWSSDK.Kinesis |
| `AWSSDK.KinesisFirehose` | AWSSDK.KinesisFirehose |
| `AWSSDK.BedrockRuntime` | AWS Bedrock |
| `OpenAI` | OpenAI |
| `Azure.AI.OpenAI` | Azure.AI.OpenAI |
| `log4net` | Log4Net |
| `NLog` | NLog |
| `Serilog` | Serilog |
| `Microsoft.Extensions.Logging` | Microsoft.Extensions.Logging |
| `NServiceBus` | NServiceBus |
| `RabbitMQ.Client` | RabbitMQ.Client |
| `MassTransit` | MassTransit |
| `Elastic.Clients.Elasticsearch` | Elasticsearch |
| `MySql.Data` | MySQL |
| `Npgsql` | PostgreSQL |
| `MongoDB.Driver` | MongoDB |

This table is not exhaustive. Always search the docs file for the package name to find the correct entry.

### Version formats in the docs

The "latest verified compatible version" appears in two formats:

**Bullet list** (Datastores, Messaging sections):
```
* Latest verified compatible version: 3.57.0
```

**Table column** (LLM, Logging sections — last `<td>` in the row):
```html
<td>
    2.8.0
</td>
```

Update only the version number. Do not change any surrounding text or structure.

## Step 7: Present changes for review

Show the user:
1. A summary table of docs updates (package, old version, new version, section)
2. Any major version bumps flagged in Step 3
3. The full diff of the docs file

**Do NOT commit or push until the user explicitly approves.**

## Step 8: Commit, push, and create PR

After user approval:

1. Commit with message: `chore(.net agent): Update compatibility docs for latest Dotty package versions`
2. Push the branch to origin (the fork).
3. Create a PR against **upstream** `newrelic/docs-website` on the `develop` branch:
   ```
   gh pr create --repo newrelic/docs-website --base develop --head tippmar-nr:<branch-name> --title "<title>" --body "<body>"
   ```
   Keep the title and body concise, e.g.:
   - Title: `chore(.net agent): Updating latest supported framework versions`
   - Body: Brief list of packages updated with new versions.
