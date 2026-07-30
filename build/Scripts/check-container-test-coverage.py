#!/usr/bin/env python
"""Guard against silent coverage holes in the Linux container test CI matrix.

Background
-----------
`.github/workflows/linux_container_tests.yml` selects which xunit test classes
run in each matrix job via a `filter` string such as
"Architecture=amd64&Distro=Ubuntu" or "Architecture=amd64&TestArea=Aws".
Selection is driven by two DISJOINT trait axes:

  - Distro    : OS-compatibility smoke tests only (Ubuntu, Alpine, Centos,
                Amazon, Fedora).
  - TestArea  : functional test groupings (Core, Messaging, Aws, Datastore).

A concrete test class is expected to be selected by EXACTLY ONE matrix entry.
Because selection depends on two independent trait keys, a test class that is
missing a trait, or that carries a TestArea/Distro value no matrix entry asks
for, silently runs in NO job at all. That is an invisible coverage hole -
worse than a flaky test, because CI stays green while the class never
executes. This script makes that failure mode loud: it statically resolves
every concrete container test class's effective traits and cross-checks them
against the matrix, failing the build if any class is selected zero times or
more than once.

It also warns (without failing) when a matrix entry selects zero classes,
since that indicates a dead/vestigial matrix entry (this happened for real:
two Debian matrix entries were removed for exactly this reason).

Inherited traits
-----------------
Traits are frequently declared on an ABSTRACT base class and inherited by one
or more concrete subclasses (see AwsSdkSQSTestBase and its two subclasses).
This script resolves each concrete class's EFFECTIVE traits by walking up its
base-class chain within the scanned source tree, with a class's own trait
always winning over an inherited one of the same key. Only concrete classes
are required to have a selector; abstract base classes are never required to
match a matrix entry themselves. Generic base classes (e.g.
`LinuxKafkaTest<T>`) are resolved by base name with generic arguments
stripped.

Usage
-----
    python build/Scripts/check-container-test-coverage.py [--verbose]

Exit code 0 on success (every concrete class matched exactly once), 1 on any
class matched zero or more than once.
"""

from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass, field

try:
    import yaml
except ImportError:  # pragma: no cover - environment guard, not a test path
    print("ERROR: PyYAML is required (pip install pyyaml)", file=sys.stderr)
    sys.exit(2)

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORKFLOW_PATH = os.path.join(REPO_ROOT, ".github", "workflows", "linux_container_tests.yml")
TESTS_ROOT = os.path.join(
    REPO_ROOT, "tests", "Agent", "IntegrationTests", "ContainerIntegrationTests", "Tests"
)
MATRIX_JOB_NAME = "linux-container-tests"

TRAIT_RE = re.compile(r'\[\s*Trait\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)\s*\]')
# Matches the start of a class declaration line, e.g.:
#   public abstract class Foo<T> : Bar<T> where T : Baz
#   public class Foo(Ctor args) : Bar<T>(args)
CLASS_START_RE = re.compile(r"^\s*(?:\w+\s+)*\bclass\b")
ABSTRACT_RE = re.compile(r"\babstract\b")
CLASS_NAME_RE = re.compile(r"\bclass\s+(\w+)")
# Captures the immediate base type name, ignoring its own generic args and
# any primary-constructor argument list, and ignoring a trailing `where`
# generic-constraint clause (which also contains a colon).
BASE_TYPE_RE = re.compile(r"class\s+\w+(?:<[^>]*>)?\s*(?:\([^)]*\))?\s*:\s*([\w.]+)")


@dataclass
class ClassInfo:
    name: str
    traits: dict[str, str]
    base: str | None
    is_abstract: bool
    file: str
    line: int


@dataclass
class MatrixEntry:
    id: str
    filter_raw: str
    requirements: dict[str, str]
    selected: list[str] = field(default_factory=list)


def strip_line_comments(lines: list[str]) -> list[str]:
    """Drop full-line `//` comments so disabled/commented-out test classes
    (a real pattern in this tree, e.g. temporarily-disabled distros) are not
    mistaken for live code. Inline trailing comments are not a pattern used
    on the declaration lines we care about, so a whole-line check suffices.
    """
    out = []
    for line in lines:
        if line.strip().startswith("//"):
            out.append("")
        else:
            out.append(line)
    return out


def parse_matrix(workflow_path: str) -> list[MatrixEntry]:
    with open(workflow_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    job = doc["jobs"][MATRIX_JOB_NAME]
    include = job["strategy"]["matrix"]["include"]

    entries = []
    for item in include:
        filter_raw = item["filter"]
        requirements = {}
        for clause in filter_raw.split("&"):
            key, _, value = clause.partition("=")
            requirements[key.strip()] = value.strip()
        entry_id = "{}/{}".format(item.get("arch", "?"), item.get("name", "?"))
        entries.append(MatrixEntry(id=entry_id, filter_raw=filter_raw, requirements=requirements))
    return entries


def scan_test_classes(tests_root: str) -> dict[str, ClassInfo]:
    classes: dict[str, ClassInfo] = {}

    for dirpath, _dirnames, filenames in os.walk(tests_root):
        for filename in sorted(filenames):
            if not filename.endswith(".cs"):
                continue
            path = os.path.join(dirpath, filename)
            with open(path, "r", encoding="utf-8") as f:
                raw_lines = f.read().splitlines()
            lines = strip_line_comments(raw_lines)

            pending_traits: dict[str, str] = {}
            i = 0
            n = len(lines)
            while i < n:
                line = lines[i]
                trait_match = TRAIT_RE.search(line)
                if trait_match:
                    pending_traits[trait_match.group(1)] = trait_match.group(2)
                    i += 1
                    continue

                if CLASS_START_RE.match(line):
                    # Accumulate the full declaration, which may span
                    # multiple lines (generic constraints, primary
                    # constructor argument lists, base type on its own
                    # line), until we hit the body-open brace or, for a
                    # primary-constructor expression-bodied class, the
                    # terminating semicolon.
                    decl_lines = [line]
                    j = i
                    while "{" not in decl_lines[-1] and not decl_lines[-1].rstrip().endswith(";"):
                        j += 1
                        if j >= n:
                            break
                        decl_lines.append(lines[j])
                    decl_text = re.sub(r"\s+", " ", " ".join(decl_lines))

                    name_match = CLASS_NAME_RE.search(decl_text)
                    if name_match:
                        class_name = name_match.group(1)
                        base_match = BASE_TYPE_RE.search(decl_text)
                        base_name = base_match.group(1) if base_match else None
                        is_abstract = bool(ABSTRACT_RE.search(decl_text.split("class", 1)[0]))

                        classes[class_name] = ClassInfo(
                            name=class_name,
                            traits=dict(pending_traits),
                            base=base_name,
                            is_abstract=is_abstract,
                            file=os.path.relpath(path, REPO_ROOT),
                            line=i + 1,
                        )

                    pending_traits = {}
                    i = j + 1
                    continue

                # Non-Trait attribute lines (e.g. [Collection("...")], [Fact])
                # or blank/other lines: keep accumulating pending traits,
                # don't reset.
                i += 1

    return classes


def effective_traits(class_name: str, classes: dict[str, ClassInfo], _seen: set[str] | None = None) -> dict[str, str]:
    """Resolve a class's effective traits by walking up its base-class
    chain (within this scanned tree only - external bases like
    NewRelicIntegrationTest<T> are not present in `classes` and simply end
    the walk). A class's own trait wins over an inherited one for the same
    key.
    """
    if _seen is None:
        _seen = set()
    if class_name in _seen:
        return {}  # defensive: guard against an accidental base-class cycle
    _seen.add(class_name)

    info = classes.get(class_name)
    if info is None:
        return {}

    inherited: dict[str, str] = {}
    if info.base:
        inherited = effective_traits(info.base, classes, _seen)

    merged = dict(inherited)
    merged.update(info.traits)
    return merged


def matches(requirements: dict[str, str], traits: dict[str, str]) -> bool:
    return all(traits.get(key) == value for key, value in requirements.items())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verbose", action="store_true", help="print full per-class listing")
    args = parser.parse_args()

    entries = parse_matrix(WORKFLOW_PATH)
    classes = scan_test_classes(TESTS_ROOT)

    concrete = {name: info for name, info in classes.items() if not info.is_abstract}

    errors: list[str] = []
    class_matches: dict[str, list[str]] = {}

    for name, info in sorted(concrete.items()):
        traits = effective_traits(name, classes)
        selected_by = [e.id for e in entries if matches(e.requirements, traits)]
        class_matches[name] = selected_by
        for e in entries:
            if e.id in selected_by:
                e.selected.append(name)

        if len(selected_by) == 0:
            errors.append(
                "  {} ({}:{}) traits={} -> selected by NO matrix entry (coverage hole)".format(
                    name, info.file, info.line, traits or "{}"
                )
            )
        elif len(selected_by) > 1:
            errors.append(
                "  {} ({}:{}) traits={} -> selected by {} matrix entries: {} (ambiguous)".format(
                    name, info.file, info.line, traits, len(selected_by), ", ".join(selected_by)
                )
            )

    dead_entries = [e for e in entries if len(e.selected) == 0]

    if args.verbose:
        print("Concrete test classes and their matrix entry:")
        for name in sorted(class_matches):
            print("  {} -> {}".format(name, class_matches[name] or "NONE"))
        print()
        print("Matrix entries and classes selected:")
        for e in entries:
            print("  {} (filter={!r}) -> {} class(es): {}".format(
                e.id, e.filter_raw, len(e.selected), ", ".join(sorted(e.selected)) or "(none)"
            ))
        print()

    if dead_entries:
        print("WARNING: {} matrix entry(ies) select zero test classes (dead entry):".format(
            len(dead_entries)))
        for e in dead_entries:
            print("  {} (filter={!r})".format(e.id, e.filter_raw))
        print()

    if errors:
        print("FAIL: container test coverage guard found {} problem(s):".format(len(errors)))
        for err in errors:
            print(err)
        print()
        print(
            "Every concrete container test class must carry (directly or via an "
            "abstract base) exactly one Architecture+Distro or Architecture+TestArea "
            "combination that matches exactly one matrix entry in "
            "linux_container_tests.yml."
        )
        return 1

    print(
        "OK: all {} concrete container test class(es) are each selected by exactly "
        "one of {} matrix entries.".format(len(concrete), len(entries))
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
