---
name: dotnet-unit-test-coverage
description: Mandatory unit-test and code-coverage rules for the .NET agent. Use when writing an implementation plan, writing or modifying unit tests, or adding/changing production code in the Core project or the NewRelic.Agent.Extensions project. Apply without being prompted by the user.
---

# .NET Agent Unit-Test and Coverage Requirements

These rules are mandatory and apply automatically -- the user does not need to
ask for tests or for coverage. Honor them when planning work, when writing or
editing tests, and when adding or changing production code.

## Scope

- **In scope (must be unit-tested):** all production code in the `Core` project
  (`src/Agent/NewRelic/Agent/Core/`) and the `NewRelic.Agent.Extensions` project
  (`src/Agent/NewRelic/Agent/Extensions/NewRelic.Agent.Extensions/`).
- **Out of scope:** the wrapper projects under
  `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/*`. They have no unit
  tests by design (covered by the Integration / Unbounded / Container test
  solutions). When adding non-trivial logic to a wrapper, lift it into a helper
  in `NewRelic.Agent.Extensions` and unit-test the helper -- see the wrapper
  guidance in `tests/claude-tests.md` and the top-level `CLAUDE.md`.

## Rule 1: New testable code gets unit tests in the same change

Adding or changing in-scope production code without accompanying unit tests is
incomplete work, not a follow-up task. Write the tests as part of the same
change, unprompted. Use TDD where practical: write the failing test first, see
it fail, implement, see it pass.

## Rule 2: Aim for 100% code coverage

When writing or modifying unit tests, cover every reachable line and branch of
the code under test:

- Include error / `catch` paths, early returns, and boundary conditions
  (just-under / just-over a cap, empty / null inputs, first-and-last iterations).
- The only acceptable uncovered code is a genuinely unreachable defensive guard
  (for example an `IsNaN` check on a value that the parser can never produce a
  NaN for, or a `default:` no input can hit). Call these out explicitly rather
  than contorting tests to fake-cover dead code.
- Do not pad coverage with assertion-free tests. Every test must verify
  behavior, not merely execute lines.

## When writing implementation plans

Bake these rules into the plan so the implementer applies them without
re-deriving them:

- In the plan's **Global Constraints** header, state: "All new/changed code in
  `Core` and `NewRelic.Agent.Extensions` must ship with unit tests in the same
  task; target 100% reachable line and branch coverage."
- Every task that creates or modifies in-scope production code must include
  explicit TDD steps (write failing test -> run/fail -> implement -> run/pass ->
  commit) with the actual test code, not a "write tests for the above"
  placeholder.
- A task that only touches wrapper code (and cannot have its logic lifted into a
  helper) is the sole exception -- note that it is covered by integration tests
  instead.

## Mechanics

Test layout, frameworks (NUnit + JustMock Lite, interfaces/virtual only), the
`SolutionDir` build caveat, and the "run against the built DLL for
`NewRelic.Agent.Extensions.Tests`" caveat are in the top-level `CLAUDE.md`
("Building and testing from the CLI", "Testing conventions") and
`tests/claude-tests.md`. Never use `InternalsVisibleTo` to reach non-public
code -- expose a proper testable surface instead.
