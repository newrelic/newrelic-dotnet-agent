#!/usr/bin/env python3
"""Insert [Trait("Runtime", ...)] above each test class named in a lane report.

Usage: apply-runtime-traits.py <report.tsv> <source-root> [--check]

The report is produced by the RuntimeLaneReport test with
NR_RUNTIME_LANE_REPORT set. Idempotent: a class that already carries a Runtime
trait is left alone. --check makes no edits and exits 1 if any would be made.

Source files in this tree are CRLF; edits preserve that, and preserve a
leading UTF-8 BOM on any file that has one. Only the inserted trait line
itself is required to be plain ASCII.
"""

import re
import sys
from pathlib import Path

TRAIT = 'Runtime'
NEWLINE = '\r\n'
BOM = b'\xef\xbb\xbf'


def parse_report(path):
    entries = []
    for line in Path(path).read_text(encoding='ascii').splitlines():
        if not line.strip():
            continue
        full_name, lane = line.split('\t')
        if lane in ('Core', 'Framework'):
            entries.append((full_name, lane))
    return entries


def read_source(path):
    """Return (lines, had_bom) for a .cs file, preserving its existing bytes."""
    raw = path.read_bytes()
    had_bom = raw.startswith(BOM)
    text = raw.decode('utf-8-sig')
    return text.splitlines(), had_bom


def write_source(path, lines, had_bom):
    data = (NEWLINE.join(lines) + NEWLINE).encode('utf-8')
    if had_bom:
        data = BOM + data
    path.write_bytes(data)


CLASS_HEAD = re.compile(r'^(\s*)(?:public|internal)\s+(?:sealed\s+|partial\s+)*class\b(.*)$')
BARE_NAME = re.compile(r'^\s*([A-Za-z0-9_]+)')


def find_class_declarations(lines):
    """Yield (line index, indent, class name) for each class declaration.

    Handles the normal single-line form ("public class Name : Base") and
    this repo's split style, where "class" ends the line and the name (with
    its base-type clause) starts the next line.
    """
    for i, line in enumerate(lines):
        m = CLASS_HEAD.match(line)
        if not m:
            continue
        indent, rest = m.group(1), m.group(2).strip()
        if rest:
            name_m = BARE_NAME.match(rest)
            if name_m:
                yield i, indent, name_m.group(1)
            continue
        if i + 1 < len(lines):
            name_m = BARE_NAME.match(lines[i + 1])
            if name_m:
                yield i, indent, name_m.group(1)


# Matches both file-scoped ("namespace X;") and block-scoped ("namespace X"
# with the opening brace on the next line) declarations.
NS_PATTERN = re.compile(r'^\s*namespace\s+([A-Za-z0-9_.]+)\s*;?\s*$')


def class_declarations_with_ns(lines):
    """Yield (line index, indent, namespace, class name) for each declaration.

    A file can hold several sequential namespace blocks (e.g. the WCF
    IIS-hosted files), so this tracks which one is active at each line
    rather than assuming a single namespace per file. Two classes in
    different namespace blocks of the same file can share a simple name
    (a pre-existing case: WCFClient_IIS_WebHTTP_ASPDiabled appears once
    under .ASPDisabled and once, apparently by copy-paste, under
    .ASPEnabled) -- callers must match on (namespace, name), not name alone.
    """
    current_ns = None
    ns_at_line = [None] * len(lines)
    for i, line in enumerate(lines):
        m = NS_PATTERN.match(line)
        if m:
            current_ns = m.group(1)
        ns_at_line[i] = current_ns
    for i, indent, name in find_class_declarations(lines):
        yield i, indent, ns_at_line[i], name


def index_declarations(root):
    """Map (namespace, simple name) -> (path, line index of the class declaration)."""
    index = {}
    for path in sorted(Path(root).rglob('*.cs')):
        if any(part in ('obj', 'bin') for part in path.parts):
            continue
        lines, _ = read_source(path)
        for i, _indent, namespace, name in class_declarations_with_ns(lines):
            if namespace:
                index.setdefault((namespace, name), (path, i))
    return index


def already_traited(lines, decl_index):
    """True if a Runtime trait sits in the attribute block above the declaration."""
    i = decl_index - 1
    while i >= 0:
        stripped = lines[i].strip()
        if stripped.startswith('['):
            if 'RuntimeLaneResolver.TraitName' in stripped or '"%s"' % TRAIT in stripped:
                return True
            i -= 1
            continue
        if stripped == '' or stripped.startswith('//') or stripped.startswith('///'):
            i -= 1
            continue
        return False
    return False


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    report, root = sys.argv[1], sys.argv[2]
    check_only = '--check' in sys.argv[3:]

    index = index_declarations(root)
    edits = {}
    missing = []
    skipped = 0

    for full_name, lane in parse_report(report):
        namespace, _, simple = full_name.rpartition('.')
        key = (namespace, simple)
        if key not in index:
            missing.append(full_name)
            continue
        path, decl_index = index[key]
        entry = edits.get(path)
        if entry is None:
            lines, had_bom = read_source(path)
            entry = {'lines': lines, 'had_bom': had_bom, 'dirty': False}
            edits[path] = entry
        lines = entry['lines']
        # Re-find the declaration; earlier inserts shift later line numbers.
        # Match on (namespace, name): a simple name alone can be ambiguous
        # within one file (see class_declarations_with_ns).
        found = next(((i, ind) for i, ind, ns_, name in class_declarations_with_ns(lines)
                      if ns_ == namespace and name == simple), None)
        if found is None:
            missing.append(full_name)
            continue
        target, indent = found
        if already_traited(lines, target):
            skipped += 1
            continue
        trait_line = '%s[Trait("%s", "%s")]' % (indent, TRAIT, lane)
        assert all(ord(c) < 128 for c in trait_line), 'inserted line must be ASCII: %r' % trait_line
        lines.insert(target, trait_line)
        entry['dirty'] = True

    changed = 0
    for path, entry in edits.items():
        if not entry['dirty']:
            continue
        changed += 1
        if not check_only:
            write_source(path, entry['lines'], entry['had_bom'])

    print('classes in report: %d' % len(parse_report(report)))
    print('files changed:     %d' % changed)
    print('already traited:   %d' % skipped)
    if missing:
        print('NOT FOUND in source (%d):' % len(missing))
        for name in missing:
            print('  ' + name)
        return 1
    if check_only and changed:
        print('--check: %d file(s) would change' % changed)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
