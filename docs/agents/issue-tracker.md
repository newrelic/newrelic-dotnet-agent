# Issue tracker: Jira

Issues and PRDs for this repo live in Jira, not GitHub Issues. The GitHub repo
(`newrelic/newrelic-dotnet-agent`) is a public mirror for code and pull
requests; planning and triage happen in Jira.

- **Site**: new-relic.atlassian.net
- **Project key**: `NR`
- **Access**: the Atlassian MCP tools (`mcp__plugin_nr_atlassian-jira__*`). The
  site host `new-relic.atlassian.net` works directly as the `cloudId` argument
  for those tools.

## Write formatting (mandatory)

Every Jira write -- create, edit, comment -- must use `contentFormat: "adf"`
with a proper ADF (Atlassian Document Format) JSON body, even for plain text.
Follow the `nr:jira-adf-format` skill. Passing raw markdown/plain strings is
the most common mistake.

## Conventions

- **Create an issue**: `createJiraIssue` with `projectKey: NR`, an
  `issueTypeName` (`Task`, `Bug`, `Story`), a `summary`, and a `description`.
  New issues are assigned to the "APM+ -> .NET Agent" team (team field ID
  `ea229518-a006-4d09-b8c0-223a885aeff7-188`) via `additional_fields` unless
  told otherwise.
- **Read an issue**: `getJiraIssue` with the issue key (e.g. `NR-574460`).
  Include `comment` in `fields` (or fetch `*all`) to get comments.
- **List / search issues**: `searchJiraIssuesUsingJql`, e.g.
  `project = NR AND labels = "needs-triage" ORDER BY created DESC`.
- **Comment on an issue**: `addCommentToJiraIssue`.
- **Apply / remove labels**: `editJiraIssue`, setting the `labels` field (see
  `triage-labels.md` for the role strings).
- **Transition state**: `getTransitionsForJiraIssue` to find the transition ID,
  then `transitionJiraIssue`.
- **Link issues**: `createIssueLink` (`getIssueLinkTypes` lists types; for
  "A is blocked by B", inwardIssue = B, outwardIssue = A).

## Referencing tickets in git

Never link to Jira in commits or PR descriptions -- this is a public
open-source repo and Jira is internal. Reference by ticket ID only, inline in
prose (e.g. "from NR-574460"), never as a `Resolves` keyword.

## When a skill says "publish to the issue tracker"

Create a Jira issue in project `NR`.

## When a skill says "fetch the relevant ticket"

Read the Jira issue by key with `getJiraIssue`. The user will normally pass the
key (e.g. `NR-574460`) directly.

## Pull requests as a triage surface

**No.** External GitHub PRs are not pulled into the triage queue.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single Jira issue with **child** issues
as tickets.

- **Map**: a Jira issue (type `Task`/`Story`) holding the Notes /
  Decisions-so-far / Fog body; label it `wayfinder:map`.
- **Child ticket**: a Jira issue linked to the map (a sub-task under the map,
  or a `Relates` link plus `Part of NR-<map>` in the body). Label
  `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed,
  assign to the driving dev.
- **Blocking**: use Jira issue links -- "is blocked by" (`createIssueLink` with
  type `Blocks`: inwardIssue = blocker, outwardIssue = blocked). A ticket is
  unblocked when every blocker is Done/closed.
- **Frontier query**: JQL for the map's open children with no open blocker and
  no assignee; first in map order wins.
- **Claim**: assign the issue to the driving dev -- the session's first write.
- **Resolve**: comment the answer, transition the issue to Done, then append a
  context pointer (gist + link) to the map's Decisions-so-far.
