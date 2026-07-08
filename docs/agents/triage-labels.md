# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those
roles to the actual label strings used in this repo's issue tracker. For this
repo they are applied as **Jira labels** in project `NR` (see
`issue-tracker.md`).

| Role (in the skills) | Label in our tracker | Meaning                                  |
| -------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`       | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`         | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`    | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`    | `ready-for-human`    | Requires human implementation            |
| `wontfix`            | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), use the
corresponding label string from this table. Set the labels via `editJiraIssue`
on the `labels` field.

Edit the right-hand column to match whatever vocabulary you actually use. If you
prefer to drive triage through Jira workflow statuses instead of labels, record
the status names here and treat "apply label X" as "transition to status Y".
