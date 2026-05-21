# Profiler Dual-Build — Alpine Telemetry Addendum

**Companion to** `PROFILER_DUAL_BUILD_DESIGN.md` (lives on branch `spike/profiler-musl-libstdcxx-migration`). This addendum updates the Alpine customer-impact analysis with real telemetry; the prior analysis was based on team experience without numbers.

## Telemetry — Alpine apps by install type (last 30 days, queried 2026-05-21)

| Install type | Apps | Share |
|---|---:|---:|
| **k8s-operator** | 29,546 | 47.3% |
| **Tar** (tarball) | 25,110 | 40.2% |
| **NugetAgent** | 8,109 | 13.0% |
| Unknown | 121 | 0.2% |
| **Deb** | 82 | **0.13%** |
| **Total** | **62,499** | |

Source query (NRDB, `DotnetMetadataSummary`, faceted by `install_type`):

```json
{
  "facets": [
    { "name": "k8s-operator", "results": [{ "uniqueCount": 29546 }] },
    { "name": "Tar",          "results": [{ "uniqueCount": 25110 }] },
    { "name": "NugetAgent",   "results": [{ "uniqueCount":  8109 }] },
    { "name": "Unknown",      "results": [{ "uniqueCount":   121 }] },
    { "name": "Deb",          "results": [{ "uniqueCount":    82 }] }
  ],
  "totalResult": { "results": [{ "uniqueCount": 62499 }] },
  "rawSince": "1 MONTHS AGO",
  "rawUntil": "NOW"
}
```

Headline: Alpine is the .NET agent's #2 distro by app count. The .deb/.rpm install-time auto-migration that the original design treated as the load-bearing Alpine mitigation only covers ~0.13% of the actual Alpine population.

## Phase-by-phase customer impact for Alpine

### Phase 2 (this work — PR #3599, #3601, #3602, #3603) — non-breaking for every Alpine population

| Population | Today | Phase 2 outcome |
|---|---|---|
| k8s-operator (47%) | Operator sets flat `CORECLR_PROFILER_PATH`; init container's tarball is glibc-only; loaded via lazy-binding luck | Init container's tarball now has per-RID dirs + flat-path symlink → `linux-x64/`; operator still sets the flat path; resolves to glibc binary; **same lazy-binding luck as today** |
| Tar (40%) | Tarball extracted manually; flat-path is glibc binary; loaded via luck | Tarball ships with pre-baked symlink → `linux-x64/`; flat path resolves to glibc binary; **same luck as today**. If customer sources `setenv.sh` once, refresh logic re-points to musl. |
| NugetAgent (13%) | NuGet's flat `newrelic/libNewRelicProfiler.so` is glibc; loaded via luck | NuGet ships per-RID + flat compat copy (= glibc x64); flat path keeps working; **same luck as today**. Per-RID musl path becomes opt-in. |
| Deb (0.13%) | Glibc binary loaded via luck | postinst libc-aware symlink → musl; **clean auto-migration** |

**Net Phase 2 Alpine break: 0%.** Glibc binary's four-property invariant is preserved (Ubuntu 14.04 base, libc++ static, max GLIBC 2.17, lazy binding) so lazy-binding luck still works for everyone the postinst doesn't auto-migrate.

### Phase 3 (modernize glibc base) — breaks ~99.7% of Alpine

Phase 3's stated goal is to raise the glibc baseline (e.g., to Ubuntu 18.04 / glibc 2.27 once .NET 10 is the floor). At that point the glibc binary references GLIBC_2.18+ symbols → musl `dlopen` fails loudly → every Alpine app still on the flat-path-via-luck path breaks at agent attach.

| Population | Phase 3 break unless action | What "action" means |
|---|---|---|
| k8s-operator (47%) | **Yes** | Customer adds `instrumentation.newrelic.com/dotnet-runtime: linux-musl-x64` annotation to their pods AND the operator supports it (separate-repo PR) |
| Tar (40%) | **Yes** | Customer sources `setenv.sh` once OR re-extracts tarball OR updates hardcoded `CORECLR_PROFILER_PATH` |
| NugetAgent (13%) | **Yes** | Customer updates Dockerfile to copy `linux-musl-x64/libNewRelicProfiler.so` OR uses `dotnet publish -r linux-musl-x64` |
| Deb (0.13%) | No | Already auto-migrated by Phase 2 postinst |
| **Total at risk** | **~99.7%** | |

87% of Alpine (k8s-operator + Tar) has **no automatic migration trigger we control** — operator pods don't run postinst, tarball customers rarely source `setenv.sh`. Outreach can shrink the break population but not eliminate it.

## New mitigation options to weigh

### A. Agent-startup auto-migration

Add managed-side code to `NewRelic.Agent.Core.dll` that, at first attach:

1. Detects host libc (`/proc/self/maps` or `/etc/os-release`).
2. Reads `CORECLR_PROFILER_PATH`.
3. If the loaded profiler doesn't match the host libc, atomically re-points the flat-path symlink at the home root to the libc-aware variant.
4. Logs `WARN`: "migrated profiler symlink from X to Y; restart the host process to pick it up."

**Effect:** doesn't fix the current process (CLR has already `dlopen`ed the wrong binary), but every subsequent process startup picks up the corrected symlink. For ephemeral workloads (Lambda, short-lived pods, K8s rolling restarts), the migration window is short.

**Pros:** zero customer action; auto-migrates K8s and tarball populations on next process restart.
**Cons:** requires fs write to agent home dir — many container images mount it read-only. Edge case: customer who explicitly *wants* the glibc binary on Alpine for some reason.
**Estimated coverage:** ~50% of Alpine (probably half of K8s + Tar populations have writable agent home), so ~30K apps auto-migrate within one restart cycle.

### B. In-product nag (deprecation warning)

Agent logs `WARN` (or `ERROR`) at startup when it detects host=Alpine AND loaded=glibc-binary. Visible in customers' New Relic logs UI.

**Pros:** trivial to implement; doesn't break anything; drives self-service migration over time.
**Cons:** still 100% customer-action-required at Phase 3 ship time; just shrinks the surprise.

### C. Don't ship Phase 3

Keep the existing glibc build (Ubuntu 14.04, libc++ static, max GLIBC 2.17, four-property invariant) indefinitely. Land the toolchain reliability fixes that *don't* change binary properties — modern clang, modern cmake from system repos, keyring-based apt — but preserve the GLIBC ceiling.

**Pros:** zero Alpine break, ever. The .NET agent's Alpine compatibility tomorrow is exactly what it is today.
**Cons:** can't track .NET's portable build base if it diverges from glibc 2.17. `strtoll_l` latent fragility persists. Doesn't unlock libstdc++ migration on the glibc path (musl path already migrated in Phase 1).

## Updated recommendation

1. **Ship Phase 2 unchanged.** Non-breaking for every Alpine population. Adds proper musl binary as opt-in for K8s/Tar/NugetAgent customers; auto-migrates the .deb minority.
2. **Add Mitigation A (agent-startup auto-migration) as a Phase 2.5 sub-PR.** Highest-leverage cushion for the K8s and Tar populations. Even partial coverage (read-write agent home only) is tens of thousands of apps auto-migrated.
3. **Add Mitigation B (in-product nag) at the same time.** Trivial cost; drives the long tail.
4. **Land the K8s operator annotation in the same release cycle as Phase 2.** Required so the 47% K8s population has a migration path at all.
5. **Phase 3 waits for the next major version.** No pre-announce on a minor; make Phase 3 the major-version flagship. AL2 / RHEL7 drops happen at the same time — same major-version cover.
6. **Strongly consider option C (don't ship Phase 3) as the new default.** Phase 3's stated wins (modern toolchain on the glibc path, libstdc++ migration on glibc) are internally desirable but not customer-visible. If the cost is breaking 60K+ Alpine apps, the math may simply not work — and the toolchain reliability fixes can land *without* changing the glibc binary's properties.

## Open verification gap

The `AlpineX64` / `AlpineArm64` container test fixtures hardcode the flat path and don't source `setenv.sh`. With Phase 2's flat-path placeholder (PR #3599) those fixtures still resolve to the glibc binary and pass via lazy-binding luck — they never actually exercise the new musl-native binary in CI. Add at least one fixture variant that explicitly uses `CORECLR_PROFILER_PATH=$NRHOME/linux-musl-x64/libNewRelicProfiler.so` so the musl binary has real CI coverage before any Phase 3 conversation. Tracking as a follow-up to PR #3603.

---

*This addendum reflects telemetry queried 2026-05-21. The original `PROFILER_DUAL_BUILD_DESIGN.md` (on `spike/profiler-musl-libstdcxx-migration`) treated the .deb postinst as the load-bearing Alpine mitigation — telemetry shows the .deb population is 0.13% of Alpine and that mitigation alone is insufficient.*
