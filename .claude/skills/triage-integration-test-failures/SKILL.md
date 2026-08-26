---
name: triage-integration-test-failures
description: Use when a CI integration, unbounded, or container test job fails intermittently, passes on a rerun, fails identically across every matrix variant, or shows an infra-shaped error (connection timeout, account lock, native crash, docker daemon not ready) rather than a clear assertion diff -- the cause may be environmental, tooling, or a real product defect exposed only sometimes. Classify the failure before proposing any fix.
---

# Triage integration test failures

## Overview

A failure investigation ends with one of five remedies, not a patch guessed
under time pressure. Classify the failure first. The category decides the
remedy; the wrong category produces a fix that treats a symptom.

**REQUIRED BACKGROUND:** superpowers:systematic-debugging governs root-cause
work in general. This skill adds the classification step and the toolkit
specific to this repo's CI, agent, and test infrastructure.

## Hard rule: delegate the reading

A CI job log, an `az monitor`/`kubectl` dump, and an agent log are evidence,
not the deliverable. None of them belongs in the main session.

- Run every command in the toolkit below inside a subagent (Agent tool --
  general-purpose or Explore), not in the main session. Give the subagent
  the exact command and the exact question ("did this job fail on a
  connection error before any agent code ran, or on an assertion diff?");
  it returns a short verdict with the one or two evidence lines that prove
  it, never the raw log or the full command output.
- This applies even to a "quick look" -- a CI log or agent log runs to
  hundreds of KB or has single lines tens of KB wide, and once that text
  lands in the main session it is re-billed on every later turn for the
  rest of the investigation.
- For agent-log evidence specifically, dispatch a subagent that follows the
  **analyze-dotnet-agent-logs** skill; do not hand-parse the log yourself
  even inside that subagent.
- The main session's job is Steps 2-4: collect verdicts, classify, decide,
  and record the decision. It holds conclusions, not evidence bytes.

## Step 1: is it even a flake?

- Same PR, same test, one run failed and a rerun passed with no code change:
  flake. Continue.
- Fails on every run, or only after a real code change: this is a
  regression, not a flake. Use superpowers:systematic-debugging instead.
- Check `gh run list` for the workflow: has this exact test or job failed
  before on unrelated PRs? Count occurrences of the exact signature across
  recent scheduled runs, and state the rate as per-run, not per-call -- one
  occurrence in 100 runs is not proof of absence. A recurring signature
  across unrelated changes is strong evidence of an environmental cause, not
  a code bug.

## Step 2: classify by evidence, not guess

Before matching a row, trace the failing message or exception string to the
code that emits it, and note whether the assertion sits in the test body or
in fixture setup/teardown (e.g. `RemoteApplicationFixture.TestForKnownProblems`
is a health check, not the test's own logic) -- this decides which row
applies.

| Category | Signature | Evidence to pull |
|---|---|---|
| **Infra / cluster** | Whole class of tests fails identically across every framework/OS variant in the same run; error is a connection, timeout, or account/lock error thrown before any agent code runs | `az monitor metrics list` on the AKS node and load balancer; `kubectl get pods -n unbounded-services -o wide` for restarts/uptime; does a bare retry pass (transient) or fail identically (persistent server state)? |
| **Timing / race** | Assertion is a count or an event that "usually" arrives, on a harvest or aggregator cycle; failure shows the right data, late or short by one cycle | The agent log, via **analyze-dotnet-agent-logs** -- confirm the exact interleaving (e.g. Seen vs Sent counts) before touching the test |
| **Tooling / dependency bug** | Crash signature is inside a third-party tool's native code, not in test or agent code; all tests report `Passed`, only the collector/runner process dies | The crash stack (module name, access-violation code); the tool's own changelog/issue tracker for a fix version already in flight |
| **External / upstream** | Error surfaces in CI infrastructure setup (runner boot, docker daemon, VM image), not in the app or the agent at all | Search the CI vendor's own issue tracker (e.g. `actions/runner-images`) for the exact error string before assuming it's local |
| **Product defect, exposed nondeterministically** | The traced assertion is a health check or invariant (fixture setup/teardown), or the emitting code shows a structural hole (missing try/finally, unguarded async race) | The suspect code path traced above; a repro run that proves the path executed (see Toolkit) |

Never conclude "just flaky" without pulling at least one row's evidence. See
[[feedback_no_guessing]] -- a root cause claim needs a log line, a metric, or
a matching upstream issue behind it, not a hunch. An assertion that exists to
catch a product defect is never reclassified as timing/race and its
threshold is never widened -- trace and fix the defect instead.

## Step 3: pick the remedy for the category

| Category | Remedy | Do NOT |
|---|---|---|
| Infra, transient (network blip) | CI-level retry-once on the job, plus raise the client-side timeout that actually stalled; document as an accepted risk | Silently widen unrelated timeouts repo-wide |
| Infra, persistent server state (account lock, stuck pod) | Operational fix (restart the pod/service); decide explicitly whether to harden the exposure, and record that decision even if the answer is "no, too costly" | Write code to retry around infra state that a retry cannot clear |
| Timing / race in event-harvest assertions | A race-free wait keyed on the **exact asserted evidence** (e.g. an `AgentLogBase.WaitForMetricAggregateCallCount`-style helper polling the real aggregate) | Bump a harvest-cycle magic number again -- each prior bump on the same test is evidence the race is still there, only the window changed |
| Tooling bug with no correctness impact | Wait for the upstream version bump already in flight; record the specific version and re-check after it lands | Change unrelated project settings (e.g. PDB format) as a workaround before confirming the bump doesn't already fix it |
| External/upstream CI regression | Apply the vendor's own recommended workaround inside the repo's CI composite action/step; link the upstream issue in a comment; remove the workaround once fixed upstream | Invent a bespoke workaround when the vendor already published one |
| Product defect, exposed nondeterministically | Trace the code path, file a ticket, leave the check alone; hand off to superpowers:systematic-debugging with the gathered evidence as the starting hypothesis and stop triage | Reclassify as timing/race, widen the assertion or its timeout, or suppress it to keep CI green |

## Step 4: record the decision

Every one of the categories above ends in a decision someone will ask about
again ("why don't we just fix the LB exposure", "did coverlet ever get
bumped"). Capture, in a project memory or handoff note:

1. The failure signature and the run/job that showed it.
2. The evidence that ruled out the other categories.
3. The remedy chosen, and what was explicitly rejected and why.
4. Anything left open (a version to watch for, a follow-up not started).

## Toolkit quick reference

Each row runs inside a subagent per the hard rule above; the main session
sees only the verdict it returns.

- Both `gh run view --job <id> --log-failed` and `--log` truncate silently
  past ~889 lines / 168KB, cutting off mid agent-log dump. For the complete
  text: `gh api repos/<org>/<repo>/actions/jobs/<jobid>/logs`. Copy any log to
  a file before a step that clears it -- lost evidence cannot be re-created.
  The pristine agent log also survives as CI artifact
  `integration-test-results-<matrix>` (`all_solutions.yml` uploads
  `C:\IntegrationTestWorkingDirectory\**\*.log` unconditionally).
- `az monitor metrics list --resource <aks-or-lb-id> --metric <name>` --
  cluster/LB health at the failure timestamp (`MSYS_NO_PATHCONV=1` needed for
  the slash-prefixed resource ID in Git Bash).
- `kubectl get pods -n unbounded-services -o wide` -- pod restarts/uptime
  after `az aks get-credentials`.
- **analyze-dotnet-agent-logs** skill -- agent-side evidence (harvest
  timing, Seen/Sent counts, connect sequence) from the test's own log.
- **run-integration-tests** skill -- reproduce the failing test locally
  before trusting a fix. A null result only counts if the code path is
  proven to have executed (log entry/exit counts); when the agent itself is
  a suspect, rerun once with the agent detached to isolate the confound.
  Delegate the run itself, per that skill's own subagent guidance; take back
  the summary, not the console output.

## Common mistakes

- Treating "all variants failed the same way" as proof of a code bug -- it
  is usually the opposite: identical failure across unrelated variants
  points at a shared external dependency, not the code under test.
- This pattern covers whole-class failures across unrelated variants only --
  a single test failing once, or a fixture health-check assertion firing, is
  not covered by it and may be the product-defect row instead.
- Fixing the assertion's timeout/retry count instead of the mechanism that
  makes the wait racy. If a similar bump has already happened once on this
  test, that is the signal to stop bumping and find the mechanism.
- Closing the investigation without writing down why the infra issue was
  not hardened -- the same question comes back next time the flake recurs.
- Reading a CI log or agent log directly in the main session "just this
  once" -- delegate it every time; a single raw read is how a multi-step
  investigation quietly fills the context window.
