# Profiler Dual-Build (glibc + musl) -- SUSPENDED

**Status:** Suspended 2026-06-15. Not shipped. Deferred to the next major
version of the .NET agent.

**Start here when resuming.** This is the entry-point / handoff document for the
Linux profiler dual-build (glibc + musl) effort. It captures the decision to
suspend, the exact state of the code, what is done, what is not, and the
concrete steps to pick the work back up. The supporting design material is
co-located in this same directory (see "Companion documents" below).

---

## The decision (2026-06-15)

**Defer the Alpine-specific (musl-native) profiler build -- and therefore the
whole dual-build feature -- to the next major release of the .NET agent.**

Rationale: shipping the dual-build only delivers its real value once the glibc
build base is also modernized (the design's "Phase 3"). Phase 3 raises the
glibc symbol-version floor, which makes today's glibc profiler stop loading on
Alpine. Analysis of observed install-method usage showed that change would break
the overwhelming majority of the agent's Alpine population, and that the
populations affected (Kubernetes operator, tarball) have no automatic migration
trigger we control. That is too much customer impact to absorb in a minor
release, so the feature needs major-version cover and the accompanying
deprecation / migration runway.

The full customer-impact analysis that drove this is in
[`alpine-addendum.md`](alpine-addendum.md). The short version:

- Alpine is a significant Linux target for the agent.
- The dominant Alpine install methods are the Kubernetes operator and the
  tarball; the NuGet consumer package is a meaningful minority; .deb/.rpm is a
  negligible fraction.
- **Phase 2 (the merged work) is non-breaking for every Alpine population.**
  Nothing that has been built breaks anyone today.
- **Phase 3 (modernize the glibc base) breaks nearly all of Alpine** unless each
  customer takes action. The dominant methods (operator + tarball) have no
  migration trigger we control (operator pods do not run postinst; tarball
  customers rarely source `setenv.sh`).

Because the merged Phase 2 work is non-breaking, suspending is low-risk: the
feature branch can sit until the next major without harming anyone, and the
glibc-on-Alpine "lazy-binding luck" mechanism that ships today is fully
preserved.

---

## What this changes operationally

- **The feature branch `feature/profiler-dual-build-phase2` does NOT merge to
  `main` and does NOT ship** until the next major is on the table. Keep the
  branch alive; do not delete it.
- **Phase 1 already shipped on `main`** (PR #3597, the musl build target) and
  stays. It only adds the ability to *build* the musl binary in CI; it changes
  nothing about what gets packaged or loaded, so it is inert and harmless on
  its own.
- No customer-facing change has shipped. Nothing to announce, roll back, or
  deprecate right now.

---

## TL;DR of current state

Phase 2 was split into 4 sub-PRs, all of which are **merged into the feature
branch** `feature/profiler-dual-build-phase2`. The feature branch is **not**
merged to `main` (by design, now reinforced by the suspension decision). Phase
1 is on `main`. Phase 3 was never started -- it is the part that would break
customers and is the reason for the major-version deferral.

```
main ............ Phase 1 (#3597 musl build target) + ongoing unrelated work
  |
  +-- feature/profiler-dual-build-phase2 ... Phase 2 PR1-PR4 (4 squash commits)
        (4 ahead of main's branch-point; 18 behind current main -- needs a
         main merge on resume)
```

---

## PR table (all merged to the FEATURE branch, not main)

| PR | Phase | Squash SHA | What it did |
|---|---|---|---|
| #3597 | Phase 1 | `28ff855ba` (on **main**) | Add musl-native profiler build target (CI can build the musl `.so`; no packaging/load change). |
| #3599 | Phase 2 PR1 | `71cdf2cc7` | Per-RID home-directory layout (`linux-{arch}/`, `linux-musl-{arch}/`) + flat-path placeholder. |
| #3601 | Phase 2 PR2 | `d725c7018` | Libc-aware compat symlinks (`setenv.sh`, `run.sh`, deb/rpm postinst, tarball pre-bake). Default points at the glibc variant. |
| #3602 | Phase 2 PR3 | `091f387ba` | `NewRelic.Agent` consumer NuGet per-RID layout + AzureSiteExtension cleanup. |
| #3603 | Phase 2 PR4 | `984192bfc` | AlpineArm64 container test fixture + the Alpine musl-symlink redirect fix (see below). |

### PR4 detail (the last thing that happened before suspension)

PR4 added an `AlpineArm64ContainerTestFixture`. Its CI initially failed: the
Alpine/arm64 container timed out waiting for an agent log. Root cause was that
the per-RID home dir ships a default `libNewRelicProfiler.so` symlink pointing
at the **glibc** variant (`linux-arm64/`), and Alpine arm64 Docker images carry
**no glibc compatibility layer** (unlike Alpine x64), so the glibc profiler
silently refuses to load and the CLR never calls back. The fix, in
`ContainerApplication.CopyToRemote()`, detects an Alpine distro tag and
repoints the root symlink to the musl-native binary
(`linux-musl-{arch}/libNewRelicProfiler.so`) before the container starts. With
that, PR4 went green and merged. See
`tests/.../ContainerIntegrationTests/Applications/ContainerApplication.cs`.

This fix is also the first piece of **real CI coverage of the musl binary** --
see the "verification gap" note under open items.

---

## Branches and where things live

- **Feature branch:** `feature/profiler-dual-build-phase2` (off `main`). All
  Phase 2 work lives here. Holds until the next major. **Do not delete.**
- **Worktree:** `.claude/worktrees/profiler-dual-build-phase2` -- local
  checkout of the feature branch, kept for convenience.
- **Spike branch:** `spike/profiler-musl-libstdcxx-migration` -- where the
  design docs and the musl build spikes originally lived. The docs have now
  been copied into this directory (`docs/profiler-dual-build/`) so they survive
  independently of that branch. The spike branch also holds
  `PROFILER_MODERNIZATION_PLAN.md` (a precursor, broader-scope profiler-cleanup
  plan) and `MuslDockerfile` experiments; pull from it if you need that history.
- **Sub-PR branches** (`pr/profiler-dual-build-phase2/01..04`): squash-merged
  into the feature branch. Safe to leave or delete; history is preserved in the
  squash commits above.

---

## Companion documents (this directory)

- [`design.md`](design.md) -- the master design. How OpenTelemetry .NET does
  the dual-build, the recommended approach for our agent, the full phased plan
  (Phase 0-4), packaging-pipeline impact, the complete per-platform customer
  impact assessment, the must-have mitigation list, and a provenance section
  classifying every claim (verified vs. asserted vs. general-knowledge). **Read
  this first when resuming the technical work.**
- [`alpine-addendum.md`](alpine-addendum.md) -- the telemetry-driven Alpine
  re-analysis that changed the Phase 3 calculus and drove the suspension. Read
  this before any Phase 3 conversation; it changes the recommendation.
- [`musl-spike-report.md`](musl-spike-report.md) -- raw data from the two local
  musl build spikes (spike1: the libc++-static-not-PIC blocker; spike2: the
  full libstdc++ migration that meets all four binary properties and passes the
  smoke test). The source-level changes Phase 1 needed are enumerated here.

(These were sanitized to ASCII when copied from the spike branch; content is
otherwise verbatim. Box-drawing tree diagrams were converted to ASCII glyphs.)

---

## The phased plan, and where each phase stands

| Phase | What it is | State |
|---|---|---|
| Phase 0 | arm64 CI consolidation + native arm64 runner (prereqs) | Done, on main (pre-dates this series). |
| Phase 1 | Add musl build target (build only, no packaging/load change) | **Shipped on main** (#3597). |
| Phase 2 | Per-RID home layout + compat symlinks + consumer NuGet + test fixtures | **Done on feature branch** (#3599, #3601, #3602, #3603). Not shipped. |
| Phase 3 | Modernize the glibc build base (raise glibc floor to track .NET's portable baseline) | **Not started. The breaking phase. Deferred to next major.** |
| Phase 4 (optional) | Canonical `runtimes/<RID>/native/` NuGet layout | Not started. |

Phase 3 is gated on the agent's minimum-supported-runtime moving off .NET 8
(Phase 3 has nothing to modernize that would not break .NET 8 alignment). When
.NET 10 is the floor, the natural Phase 3 glibc target is Ubuntu 18.04 /
glibc 2.27 (tracks Microsoft's portable .NET 10 build base). Details and the
alternatives table are in [`design.md`](design.md) under "Phase 3".

---

## Open items to resolve when resuming

In rough priority order. Items 1-4 are the cross-repo / strategic blockers; the
rest are engineering follow-ups already scoped in the design docs.

1. **Re-make the Phase 3 ship/no-ship decision with fresh telemetry.** The
   addendum's strongest alternative is option C -- *do not ship Phase 3 at
   all*: keep the glibc binary's four-property invariant (max GLIBC 2.17, libc++
   static, narrow DT_NEEDED, lazy binding) indefinitely, and land only the
   toolchain-reliability fixes that do not change binary shape. Phase 3's wins
   are internal (modern toolchain, libstdc++ on the glibc path) and not
   customer-visible; the cost is breaking the bulk of the Alpine base. Decide
   this before building anything in Phase 3.
2. **Mitigation A -- agent-startup auto-migration.** Managed-side code that, at
   attach, detects host libc and re-points the flat-path symlink to the correct
   variant (effective on next process restart). Highest-leverage cushion for the
   k8s + tarball populations; auto-migrates a substantial share of Alpine
   (writable agent home only). Could be a "Phase 2.5" sub-PR. Detail in
   [`alpine-addendum.md`](alpine-addendum.md).
3. **Mitigation B -- in-product nag.** Startup WARN when host=Alpine AND
   loaded=glibc binary. Trivial; drives self-service migration over time. Do
   alongside Mitigation A.
4. **K8s operator annotation** (separate repo `newrelic/k8s-agents-operator`).
   Add `instrumentation.newrelic.com/dotnet-runtime` (opt-in, mirrors OTel's
   `otel-dotnet-auto-runtime`): when set, the operator builds
   `coreClrProfilerPath` from the matching per-RID subdir. Required so the
   dominant k8s Alpine population has any migration path before Phase 3. Must land in the
   same release cycle as the dual-build ship -- never lag it.
5. **Init container validation** (separate repo
   `newrelic/newrelic-dotnet-agent-init-container`). No code change expected --
   `cp -r /instrumentation/.` already preserves the per-RID subdirs and the
   symlink -- but add a tarball-shape integration test to guard the layout.
6. **Verification gap: the musl binary still has almost no CI coverage.** The
   `AlpineX64` fixture passes via lazy-binding luck against the glibc binary.
   PR4's `AlpineArm64` fix is the only path that actually exercises the
   musl-native binary, and only because Alpine arm64 has no glibc compat. Add an
   explicit fixture that sets
   `CORECLR_PROFILER_PATH=$NRHOME/linux-musl-x64/libNewRelicProfiler.so` and
   asserts the resolved path, so the musl x64 binary is covered before any
   Phase 3 work. Tracked as a PR4 follow-up.
7. **docs.newrelic.com refresh.** All Linux install pages show the flat path
   today. Needs per-RID examples + dual-build call-outs across >=7 English
   `.mdx` files plus translations, and the `newrelic/newrelic-dotnet-examples`
   Dockerfiles. Lockstep with the eventual ship, not before.
8. **`strtoll_l` / `strtoull_l` latent fragility** on the glibc binary -- Phase
   1 likely closed it on the musl binary; the glibc binary still carries it
   until source changes land (folds into Phase 3's libstdc++ migration).

---

## Things that are settled / explicitly out of scope

- **Phase 2 is non-breaking** for all four documented Linux install patterns and
  every Alpine population. The compat symlinks are load-bearing for that claim
  and are implemented (#3601).
- **Lambda:** no changes needed. Lambda is glibc-only; the layer's flat
  `CORECLR_PROFILER_PATH` resolves via the tarball-baked symlink -> `linux-x64/`.
- **Windows:** zero impact. Profiler.dll still ships one binary per arch.
- **Internal Profiler NuGet `runtimes/<RID>/native/` migration** (Phase 4):
  intentionally skipped for now. Internal-only, no customer impact, easy
  follow-up if ever wanted.
- **Fat cross-libc single binary:** rejected. The per-callback indirection cost
  is not worth saving one `.so` of disk.

---

## Critical context / gotchas for resuming

- **Profiler-touching changes need the deploy ritual** (build_profiler.yml
  deploy=true on the work branch -> merge the auto-generated NuGet-update PR ->
  full CI). Phase 1 (#3597) went through it; the resulting NuGet is on
  nuget.org. Phase 2 PRs do not touch profiler binaries, so they skipped it.
  Any Phase 3 work touches the binaries and will need it again.
- **The flat-path placeholder is a real glibc file copy on Windows-built home
  dirs**, not a symlink -- postinst / tarball-pre-bake / setenv.sh only create
  the libc-aware symlink at install time on Linux. This is by design;
  IntegrationTests run against the Windows-built home dirs.
- **`contentFiles` does not RID-resolve.** Every consumer publish gets all 5
  Linux `.so` files regardless of `-r` (~37 MB). Not a regression vs. today, but
  flag it for Lambda customers if size ever matters.
- **The feature branch is 18 commits behind `main`.** Merge `main` in before
  doing anything substantive on resume, and expect to re-validate against the
  current agent home-directory build.
- **Project memory:** `project_profiler_verification_workflow.md` (deploy
  ritual) and `feedback_linux_profiler_path_hardcoded.md` (hardcoded
  `CORECLR_PROFILER_PATH` is the dominant install pattern -- the reason the
  whole compat-symlink layer exists).

---

## Concrete next action when resuming

1. Read [`alpine-addendum.md`](alpine-addendum.md), then [`design.md`](design.md)
   Phase 3 section.
2. Re-check Alpine install-method usage (the earlier snapshot will be stale)
   and re-confirm whether Phase 3 is worth its customer cost, or whether option C
   (toolchain fixes only, no glibc-baseline lift) is the better default.
3. If proceeding: merge `main` into the feature branch, re-validate Phase 2,
   then sequence Mitigations A/B + the k8s operator annotation to land in the
   same major-version release as the glibc-baseline lift.
4. If not proceeding with Phase 3: decide whether to ship Phase 2 on its own
   (non-breaking, adds the musl binary as opt-in) in a minor, or keep the whole
   feature parked.
