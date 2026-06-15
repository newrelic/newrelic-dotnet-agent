# Profiler Dual-Build -- Alpine Customer-Impact Addendum

**Companion to** [`design.md`](design.md). This addendum refines the Alpine
customer-impact analysis using observed install-method usage; the prior analysis
reasoned about install patterns qualitatively.

## Alpine install-method mix

Alpine is a significant Linux target for the agent. Across the Alpine install
base, the dominant install methods are the **Kubernetes operator** and the
**tarball**; the **NuGet consumer package** is a meaningful minority; and the
**.deb/.rpm** packages are a negligible fraction.

That distribution is the crux of the analysis. The original design treated the
.deb/.rpm install-time auto-migration (the postinst libc-aware symlink) as the
load-bearing Alpine mitigation. Because .deb/.rpm is a negligible fraction of
the Alpine base, that mitigation alone covers almost none of the actual Alpine
population -- the methods that dominate (operator, tarball) are exactly the ones
postinst never runs against.

## Phase-by-phase customer impact for Alpine

### Phase 2 (this work -- PR #3599, #3601, #3602, #3603) -- non-breaking for every Alpine population

| Population | Today | Phase 2 outcome |
|---|---|---|
| k8s-operator | Operator sets flat `CORECLR_PROFILER_PATH`; init container's tarball is glibc-only; loaded via lazy-binding luck | Init container's tarball now has per-RID dirs + flat-path symlink -> `linux-x64/`; operator still sets the flat path; resolves to glibc binary; **same lazy-binding luck as today** |
| Tar | Tarball extracted manually; flat-path is glibc binary; loaded via luck | Tarball ships with pre-baked symlink -> `linux-x64/`; flat path resolves to glibc binary; **same luck as today**. If customer sources `setenv.sh` once, refresh logic re-points to musl. |
| NugetAgent | NuGet's flat `newrelic/libNewRelicProfiler.so` is glibc; loaded via luck | NuGet ships per-RID + flat compat copy (= glibc x64); flat path keeps working; **same luck as today**. Per-RID musl path becomes opt-in. |
| Deb | Glibc binary loaded via luck | postinst libc-aware symlink -> musl; **clean auto-migration** |

**Net Phase 2 Alpine break: none.** Glibc binary's four-property invariant is preserved (Ubuntu 14.04 base, libc++ static, max GLIBC 2.17, lazy binding) so lazy-binding luck still works for everyone the postinst doesn't auto-migrate.

### Phase 3 (modernize glibc base) -- breaks nearly all of Alpine

Phase 3's stated goal is to raise the glibc baseline (e.g., to Ubuntu 18.04 / glibc 2.27 once .NET 10 is the floor). At that point the glibc binary references GLIBC_2.18+ symbols -> musl `dlopen` fails loudly -> every Alpine app still on the flat-path-via-luck path breaks at agent attach.

| Population | Phase 3 break unless action | What "action" means |
|---|---|---|
| k8s-operator | **Yes** | Customer adds `instrumentation.newrelic.com/dotnet-runtime: linux-musl-x64` annotation to their pods AND the operator supports it (separate-repo PR) |
| Tar | **Yes** | Customer sources `setenv.sh` once OR re-extracts tarball OR updates hardcoded `CORECLR_PROFILER_PATH` |
| NugetAgent | **Yes** | Customer updates Dockerfile to copy `linux-musl-x64/libNewRelicProfiler.so` OR uses `dotnet publish -r linux-musl-x64` |
| Deb | No | Already auto-migrated by Phase 2 postinst |
| **Total at risk** | **nearly all** | |

The dominant install methods (operator + tarball) have **no automatic migration trigger we control** -- operator pods don't run postinst, tarball customers rarely source `setenv.sh`. Outreach can shrink the break population but not eliminate it.

## New mitigation options to weigh

### A. Agent-startup auto-migration

Add managed-side code to `NewRelic.Agent.Core.dll` that, at first attach:

1. Detects host libc (`/proc/self/maps` or `/etc/os-release`).
2. Reads `CORECLR_PROFILER_PATH`.
3. If the loaded profiler doesn't match the host libc, atomically re-points the flat-path symlink at the home root to the libc-aware variant.
4. Logs `WARN`: "migrated profiler symlink from X to Y; restart the host process to pick it up."

**Effect:** doesn't fix the current process (CLR has already `dlopen`ed the wrong binary), but every subsequent process startup picks up the corrected symlink. For ephemeral workloads (Lambda, short-lived pods, K8s rolling restarts), the migration window is short.

**Pros:** zero customer action; auto-migrates K8s and tarball populations on next process restart.
**Cons:** requires fs write to agent home dir -- many container images mount it read-only. Edge case: customer who explicitly *wants* the glibc binary on Alpine for some reason.
**Estimated coverage:** a substantial share of Alpine -- the portion of the K8s + Tar populations with a writable agent home -- auto-migrates within one restart cycle.

### B. In-product nag (deprecation warning)

Agent logs `WARN` (or `ERROR`) at startup when it detects host=Alpine AND loaded=glibc-binary. Visible in customers' New Relic logs UI.

**Pros:** trivial to implement; doesn't break anything; drives self-service migration over time.
**Cons:** still 100% customer-action-required at Phase 3 ship time; just shrinks the surprise.

### C. Don't ship Phase 3

Keep the existing glibc build (Ubuntu 14.04, libc++ static, max GLIBC 2.17, four-property invariant) indefinitely. Land the toolchain reliability fixes that *don't* change binary properties -- modern clang, modern cmake from system repos, keyring-based apt -- but preserve the GLIBC ceiling.

**Pros:** zero Alpine break, ever. The .NET agent's Alpine compatibility tomorrow is exactly what it is today.
**Cons:** can't track .NET's portable build base if it diverges from glibc 2.17. `strtoll_l` latent fragility persists. Doesn't unlock libstdc++ migration on the glibc path (musl path already migrated in Phase 1).

## Updated recommendation

1. **Ship Phase 2 unchanged.** Non-breaking for every Alpine population. Adds proper musl binary as opt-in for K8s/Tar/NugetAgent customers; auto-migrates the .deb minority.
2. **Add Mitigation A (agent-startup auto-migration) as a Phase 2.5 sub-PR.** Highest-leverage cushion for the K8s and Tar populations. Even partial coverage (read-write agent home only) auto-migrates a large share of apps.
3. **Add Mitigation B (in-product nag) at the same time.** Trivial cost; drives the long tail.
4. **Land the K8s operator annotation in the same release cycle as Phase 2.** Required so the dominant K8s population has a migration path at all.
5. **Phase 3 waits for the next major version.** No pre-announce on a minor; make Phase 3 the major-version flagship. AL2 / RHEL7 drops happen at the same time -- same major-version cover.
6. **Strongly consider option C (don't ship Phase 3) as the new default.** Phase 3's stated wins (modern toolchain on the glibc path, libstdc++ migration on glibc) are internally desirable but not customer-visible. If the cost is breaking the bulk of the Alpine base, the math may simply not work -- and the toolchain reliability fixes can land *without* changing the glibc binary's properties.

## Open verification gap

The `AlpineX64` / `AlpineArm64` container test fixtures hardcode the flat path and don't source `setenv.sh`. With Phase 2's flat-path placeholder (PR #3599) those fixtures still resolve to the glibc binary and pass via lazy-binding luck -- they never actually exercise the new musl-native binary in CI. Add at least one fixture variant that explicitly uses `CORECLR_PROFILER_PATH=$NRHOME/linux-musl-x64/libNewRelicProfiler.so` so the musl binary has real CI coverage before any Phase 3 conversation. Tracking as a follow-up to PR #3603.

---

*This addendum refines [`design.md`](design.md), which treated the .deb postinst as the load-bearing Alpine mitigation. Observed install-method usage shows the .deb population is a negligible fraction of Alpine, so that mitigation alone is insufficient -- the dominant operator and tarball populations are unaffected by postinst.*
