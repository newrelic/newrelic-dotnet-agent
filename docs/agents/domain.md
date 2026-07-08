# Domain Docs

How the engineering skills should consume this repo's domain documentation when
exploring the codebase. This repo is **single-context**.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root.
- **`docs/adr/`** -- read ADRs that touch the area you're about to work in.

If any of these files don't exist, **proceed silently**. Don't flag their
absence; don't suggest creating them upfront. The `/domain-modeling` skill
(reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates
them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo (this repo):

```
/
  CONTEXT.md
  docs/adr/
    0001-some-decision.md
    0002-another-decision.md
  src/
```

(A multi-context repo would instead have a `CONTEXT-MAP.md` at the root pointing
at one `CONTEXT.md` per context, plus context-scoped `src/<context>/docs/adr/`.
That layout does not apply here.)

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal,
a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift
to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal -- either
you're inventing language the project doesn't use (reconsider) or there's a real
gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than
silently overriding:

> _Contradicts ADR-0007 -- but worth reopening because..._
