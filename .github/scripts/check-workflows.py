#!/usr/bin/env python3
"""Static checks over the .github workflows that drive the test solutions.

Exits 0 when every check passes, 1 otherwise, printing one line per failure.
"""

import json
import re
import sys
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS = ROOT / ".github" / "workflows"

TOUCHED = [
    "all_solutions.yml",
    "linux_container_tests.yml",
    "test_selection.yml",
    "integration_tests.yml",
    "unbounded_tests.yml",
    "targeted_tests.yml",
]

REQUIRED_CONTEXTS = [
    "Build FullAgent and MSIInstaller",
    "Check Test Matrix Status",
    "Run ArtifactBuilder",
]

NO_CONCURRENCY = ["integration_tests.yml", "unbounded_tests.yml"]

RANK = {"none": 0, "read": 1, "write": 2}

FAILURES = []

_EXTERNAL_CACHE = {}


def fail(check, message):
    FAILURES.append("%s: %s" % (check, message))


def load(name):
    """Parse a workflow. PyYAML turns the unquoted key `on` into True."""
    try:
        return yaml.safe_load((WORKFLOWS / name).read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 - any parse error is a workflow-parse failure
        fail("workflow-parse", "%s does not parse: %s" % (name, exc))
        return None


def load_external(name):
    """Load a workflow outside TOUCHED, on demand, for the permissions ceiling check only.

    Not added to `docs` and not run through the explicit-permissions checks,
    which stay scoped to TOUCHED.
    """
    if name in _EXTERNAL_CACHE:
        return _EXTERNAL_CACHE[name]
    path = WORKFLOWS / name
    if not path.exists():
        _EXTERNAL_CACHE[name] = None
        return None
    try:
        doc = yaml.safe_load(path.read_text(encoding="utf-8"))
    except Exception:  # noqa: BLE001 - reported by the caller with context
        _EXTERNAL_CACHE[name] = None
        return None
    _EXTERNAL_CACHE[name] = doc
    return doc


def jobs_of(doc):
    return (doc or {}).get("jobs") or {}


def perm_map(value, where, check):
    """Normalize a permissions value to {scope: level}. A string form fails."""
    if value is None:
        return None
    if isinstance(value, str):
        fail(check, "%s uses the string permissions form '%s'; use an explicit mapping" % (where, value))
        return {}
    return {k: str(v) for k, v in value.items()}


def check_needs_resolve(docs):
    doc = docs.get("all_solutions.yml")
    if doc is None:
        return
    ids = set(jobs_of(doc))
    for jid, job in jobs_of(doc).items():
        needs = job.get("needs") or []
        if isinstance(needs, str):
            needs = [needs]
        for dep in needs:
            if dep not in ids:
                fail("needs-resolution", "job '%s' needs '%s', which is not a job id in all_solutions.yml" % (jid, dep))


def check_required_checks_are_bare_jobs(docs):
    doc = docs.get("all_solutions.yml")
    if doc is None:
        return
    bare = {}
    for jid, job in jobs_of(doc).items():
        if "uses" not in job and job.get("name"):
            bare[job["name"]] = jid
    for context in REQUIRED_CONTEXTS:
        if context not in bare:
            fail(
                "required-checks",
                "required status check '%s' is not a bare top-level job name in all_solutions.yml; "
                "a caller job reports as '<caller> / <called>' and would detach the check in "
                "ruleset 'main branch' (id 4599184)" % context,
            )


def check_permission_ceilings(docs):
    for name, doc in docs.items():
        if doc is None:
            continue
        top = perm_map(doc.get("permissions"), "%s top-level" % name, "permissions")
        if top:
            writes = sorted(k for k, v in top.items() if RANK.get(v, 0) >= 2)
            if writes:
                fail("permissions", "%s top-level permissions grant write scopes %s; move them to the jobs that need them" % (name, writes))
        for jid, job in jobs_of(doc).items():
            if "permissions" not in job:
                fail("permissions", "%s job '%s' has no explicit permissions block" % (name, jid))
            else:
                perm_map(job["permissions"], "%s job '%s'" % (name, jid), "permissions")

    for name, doc in docs.items():
        if doc is None:
            continue
        for jid, job in jobs_of(doc).items():
            uses = job.get("uses") or ""
            if not uses.startswith("./.github/workflows/"):
                continue
            called = uses.rsplit("/", 1)[-1]
            called_doc = docs.get(called)
            if called_doc is None:
                called_doc = load_external(called)
                if called_doc is None:
                    fail("permissions", "%s job '%s' calls %s, which does not exist or does not parse" % (name, jid, called))
                    continue
            needed = {}
            for cjob in jobs_of(called_doc).values():
                for scope, level in (perm_map(cjob.get("permissions"), called, "permissions") or {}).items():
                    if RANK.get(level, 0) > RANK.get(needed.get(scope, "none"), 0):
                        needed[scope] = level
            granted = perm_map(job.get("permissions"), "%s job '%s'" % (name, jid), "permissions") or {}
            short = {
                scope: level
                for scope, level in needed.items()
                if RANK.get(level, 0) > RANK.get(granted.get(scope, "none"), 0)
            }
            if short:
                fail(
                    "permissions",
                    "%s job '%s' grants %s but %s needs at least %s; an under-granted ceiling "
                    "fails at run time as a 403, not at parse time" % (name, jid, granted or {}, called, short),
                )


def extract_pairs_test_selection():
    text = (WORKFLOWS / "test_selection.yml").read_text(encoding="utf-8")
    match = re.search(r"container_all='(\[[^']*\])'", text)
    if not match:
        fail("container-lists", "could not extract container_all='[...]' from test_selection.yml; the anchor changed")
        return None
    try:
        return [tuple(p.split("/", 1)) for p in json.loads(match.group(1))]
    except (ValueError, json.JSONDecodeError) as exc:
        fail("container-lists", "container_all in test_selection.yml is not valid JSON: %s" % exc)
        return None


def extract_pairs_container_matrix(docs):
    doc = docs.get("linux_container_tests.yml")
    if doc is None:
        return None
    job = jobs_of(doc).get("select-matrix")
    if not job:
        fail("container-lists", "linux_container_tests.yml has no select-matrix job")
        return None
    bodies = [s.get("run", "") for s in job.get("steps") or []]
    for body in bodies:
        match = re.search(r"all='(\[.*?\])'", body, re.S)
        if match:
            try:
                entries = json.loads(match.group(1))
            except (ValueError, json.JSONDecodeError) as exc:
                fail("container-lists", "the select-matrix JSON literal is not valid JSON: %s" % exc)
                return None
            return [(e["name"], e["arch"]) for e in entries]
    fail("container-lists", "could not extract all='[...]' from the select-matrix job; the anchor changed")
    return None


def extract_list_literal(name):
    """Pull a bash single-quoted JSON array literal out of test_selection.yml."""
    text = (WORKFLOWS / "test_selection.yml").read_text(encoding="utf-8")
    match = re.search(r"%s='(\[[^']*\])'" % re.escape(name), text)
    if not match:
        fail("lane-lists", "could not extract %s='[...]' from test_selection.yml; the anchor changed" % name)
        return None
    try:
        return json.loads(match.group(1))
    except (ValueError, json.JSONDecodeError) as exc:
        fail("lane-lists", "%s in test_selection.yml is not valid JSON: %s" % (name, exc))
        return None


def check_lane_lists():
    """Each suite's lane lists must stay consistent with its canonical list.

    Selection is by inclusion, so a shard in no lane runs nowhere and reports
    green, and a stale entry names a shard that no longer exists. Neither shows
    up at run time.
    """
    for suite in ("integration", "unbounded"):
        all_shards = extract_list_literal("%s_all" % suite)
        windows_only = extract_list_literal("%s_windows_only" % suite)
        smoke = extract_list_literal("%s_core_smoke" % suite)
        if all_shards is None or windows_only is None or smoke is None:
            continue

        for label, lane in (("windows_only", windows_only), ("core_smoke", smoke)):
            stale = sorted(set(lane) - set(all_shards))
            if stale:
                fail(
                    "lane-lists",
                    "%s_%s names shard(s) absent from %s_all: %s. A renamed or deleted shard leaves a "
                    "stale lane entry that never fires" % (suite, label, suite, stale),
                )

        both = sorted(set(windows_only) & set(smoke))
        if both:
            fail(
                "lane-lists",
                "%s shard(s) %s are in both %s_windows_only and %s_core_smoke. windows_only means the shard has "
                "no Core tests; core_smoke means it has Core tests worth running on Windows. It cannot be both"
                % (suite, both, suite, suite),
            )


def check_container_lists_match(docs):
    left = extract_pairs_test_selection()
    right = extract_pairs_container_matrix(docs)
    if left is None or right is None:
        return
    if sorted(left) != sorted(right):
        only_left = sorted(set(left) - set(right))
        only_right = sorted(set(right) - set(left))
        fail(
            "container-lists",
            "container group lists have drifted. Only in test_selection.yml: %s. "
            "Only in linux_container_tests.yml: %s" % (only_left, only_right),
        )


def check_no_concurrency_in_called_workflows(docs):
    for name in NO_CONCURRENCY:
        doc = docs.get(name)
        if doc is None:
            continue
        if "concurrency" in doc:
            fail(
                "called-workflow-concurrency",
                "%s declares concurrency. github.workflow in a called workflow resolves to the "
                "caller's name, so integration_tests and unbounded_tests would share a group and "
                "cancel each other inside one run" % name,
            )


def check_no_expressions_in_action_metadata():
    """No expression in an action.yml's metadata.

    The github context is available inside `runs:` but not in the metadata block,
    so an example path in a description makes the runner refuse to load the action
    with "Unrecognized named-value: 'github'". PyYAML parses it happily.
    """
    for path in sorted((ROOT / ".github" / "actions").glob("*/action.yml")):
        try:
            doc = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
        except Exception as exc:  # noqa: BLE001 - an unparseable action is a failure
            fail("action-metadata", "%s does not parse: %s" % (path.name, exc))
            continue
        fields = [(k, doc.get(k)) for k in ("name", "description", "author")]
        for section in ("inputs", "outputs"):
            for item, spec in (doc.get(section) or {}).items():
                if isinstance(spec, dict):
                    for key in ("description", "default"):
                        fields.append(("%s.%s.%s" % (section, item, key), spec.get(key)))
        for where, text in fields:
            if isinstance(text, str) and "${{" in text:
                fail(
                    "action-metadata",
                    "%s %s contains an expression; no context is available in action "
                    "metadata, so the runner fails to load the action"
                    % (path.parent.name + "/action.yml", where),
                )


def main():
    docs = {}
    for name in TOUCHED:
        path = WORKFLOWS / name
        if not path.exists():
            fail("workflow-parse", "%s does not exist" % name)
            continue
        docs[name] = load(name)

    check_needs_resolve(docs)
    check_required_checks_are_bare_jobs(docs)
    check_permission_ceilings(docs)
    check_container_lists_match(docs)
    check_lane_lists()
    check_no_concurrency_in_called_workflows(docs)
    check_no_expressions_in_action_metadata()

    if FAILURES:
        print("check-workflows: %d failure(s)" % len(FAILURES))
        for line in FAILURES:
            print("  " + line)
        return 1
    print(
        "check-workflows: all checks passed (needs-resolution, required-checks, "
        "permissions, container-lists, lane-lists, called-workflow-concurrency, action-metadata)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
