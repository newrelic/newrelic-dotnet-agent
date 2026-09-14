#!/usr/bin/env python3
"""Export the .NET agent log triage plugin into a checkout of the GHE
marketplace repository agents/claude-skills.

Run from the repository root:
    python tools/support-log-plugin/export.py --target /path/to/claude-skills

It writes; it never commits and never pushes. Review git status in the target
and push by hand.
"""

import argparse
import datetime
import json
import os
import shutil
import subprocess
import sys
import tempfile

TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
SKILL_REL = os.path.join('.claude', 'skills', 'analyze-dotnet-agent-logs')
PLUGIN_NAME = 'dotnet-log-triage'
SKILL_NAME = 'triage-dotnet-agent-logs'
FIELD_REL = ('teams', 'dotnet-agent', 'playbooks', 'field')
UNPROMOTED_DAYS = 90
VERIFIED_ID_MAX = 99
FIELD_ID_MIN = 100


class ExportError(Exception):
    """Any condition that must stop the export before it writes."""


def repo_root():
    return os.path.normpath(os.path.join(TOOL_DIR, '..', '..'))


def skill_dir(root=None):
    return os.path.join(root or repo_root(), SKILL_REL)


def import_nrlog(root=None):
    scripts = os.path.join(skill_dir(root), 'scripts')
    if scripts not in sys.path:
        sys.path.insert(0, scripts)
    try:
        import nrlog
    except Exception as error:
        raise ExportError('cannot import nrlog.py from %s: %s'
                          % (scripts, error))
    for name in ('load_playbooks', 'default_playbook_dir'):
        if not hasattr(nrlog, name):
            raise ExportError('nrlog.py has no %s; part 1 is not complete'
                              % name)
    return nrlog


def load_verified(nrlog, root=None):
    directory = os.path.join(skill_dir(root), 'references', 'playbooks')
    if not os.path.isdir(directory):
        raise ExportError('no verified playbook directory at %s' % directory)
    try:
        books = nrlog.load_playbooks(directory)
    except (Exception, SystemExit) as error:
        raise ExportError('the verified playbook set will not load: %s'
                          % error)
    if not books:
        raise ExportError('the verified playbook set is empty at %s'
                          % directory)
    wrong = [b.path for b in books if b.tier != 'verified']
    if wrong:
        raise ExportError('not tier verified: %s' % ', '.join(sorted(wrong)))
    return books


def load_field(nrlog, target):
    directory = os.path.join(target, *FIELD_REL)
    if not os.path.isdir(directory):
        return [], []
    books = []
    warnings = []
    names = sorted(n for n in os.listdir(directory) if n.endswith('.md')
                   and n.lower() != 'readme.md')
    for name in names:
        source = os.path.join(directory, name)
        holding = tempfile.mkdtemp(prefix='nrlog-field-')
        try:
            shutil.copy2(source, os.path.join(holding, name))
            loaded = nrlog.load_playbooks(holding)
        except (Exception, SystemExit) as error:
            warnings.append('skipped %s: %s' % (name, error))
            continue
        finally:
            shutil.rmtree(holding, ignore_errors=True)
        if len(loaded) != 1:
            warnings.append('skipped %s: loader returned %d playbooks'
                            % (name, len(loaded)))
            continue
        book = loaded[0]
        if book.tier != 'field':
            warnings.append('skipped %s: tier is %s, expected field'
                            % (name, book.tier))
            continue
        book.path = source
        books.append(book)
    books.sort(key=lambda b: b.id)
    return books, warnings


def check_ids(verified, field):
    errors = []
    for book in verified:
        if book.id > VERIFIED_ID_MAX:
            errors.append('%s: verified id %d must be %d or below'
                          % (book.path, book.id, VERIFIED_ID_MAX))
    for book in field:
        if book.id < FIELD_ID_MIN:
            errors.append('%s: field id %d must be %d or above'
                          % (book.path, book.id, FIELD_ID_MIN))
    seen = {}
    for book in list(verified) + list(field):
        if book.id in seen:
            errors.append('duplicate id %d in %s and %s'
                          % (book.id, seen[book.id], book.path))
        else:
            seen[book.id] = book.path
    return errors


PLUGIN_SRC = os.path.join(TOOL_DIR, 'plugin')
SCAFFOLD = os.path.join(TOOL_DIR, 'scaffold')
HOST_PATHS = ('/home/', '/Users/', '\\Users\\')
TEXT_SUFFIXES = ('.md', '.json', '.py', '.yml', '.sh', '.ps1', '.txt')


def plugin_paths(target):
    plugin = os.path.join(target, 'plugins', PLUGIN_NAME)
    skill = os.path.join(plugin, 'skills', SKILL_NAME)
    return plugin, skill


def plugin_version(root, fallback):
    count = None
    try:
        result = subprocess.run(
            ['git', '-C', root, 'rev-list', '--count', 'HEAD', '--',
             SKILL_REL, os.path.join('tools', 'support-log-plugin')],
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, check=True)
        count = int(result.stdout.decode('ascii').strip())
    except Exception:
        return fallback, ('cannot count commits with git; keeping version %s'
                          % fallback)
    parts = fallback.split('.')
    major = parts[0] if parts else '1'
    return '%s.0.%d' % (major, count), None


def plan_copies(root, target, verified, field):
    plugin, skill = plugin_paths(target)
    pairs = [
        (os.path.join(SCAFFOLD, 'bootstrap.sh'),
         os.path.join(plugin, 'bootstrap.sh')),
        (os.path.join(SCAFFOLD, 'bootstrap.ps1'),
         os.path.join(plugin, 'bootstrap.ps1')),
        (os.path.join(SCAFFOLD, 'lint_playbooks.py'),
         os.path.join(plugin, 'lint_playbooks.py')),
        (os.path.join(PLUGIN_SRC, 'README.md'),
         os.path.join(plugin, 'README.md')),
        (os.path.join(PLUGIN_SRC, 'SKILL.md'),
         os.path.join(skill, 'SKILL.md')),
        (os.path.join(PLUGIN_SRC, 'references', 'log-basics.md'),
         os.path.join(skill, 'references', 'log-basics.md')),
        (os.path.join(skill_dir(root), 'scripts', 'nrlog.py'),
         os.path.join(skill, 'scripts', 'nrlog.py')),
        (os.path.join(skill_dir(root), 'references', 'playbooks', 'README.md'),
         os.path.join(skill, 'references', 'playbooks', 'README.md')),
    ]
    for book in list(verified) + list(field):
        pairs.append((book.path,
                      os.path.join(skill, 'references', 'playbooks',
                                   os.path.basename(book.path))))
    missing = [src for src, _ in pairs if not os.path.isfile(src)]
    if missing:
        raise ExportError('missing input file(s):\n  %s'
                          % '\n  '.join(sorted(missing)))
    return pairs


def entry_name(item):
    return str(item.get('name', '')) if isinstance(item, dict) else ''


def marketplace_template():
    path = os.path.join(SCAFFOLD, 'marketplace.json')
    if not os.path.isfile(path):
        raise ExportError('no marketplace template at %s' % path)
    with open(path, 'r', encoding='ascii') as handle:
        template = json.load(handle)
    ours = [p for p in template.get('plugins') or []
            if entry_name(p) == PLUGIN_NAME]
    if len(ours) != 1:
        raise ExportError('%s must hold exactly one %s entry'
                          % (path, PLUGIN_NAME))
    return template, ours[0]


def merge_marketplace(target):
    template, entry = marketplace_template()
    dest = os.path.join(target, '.claude-plugin', 'marketplace.json')
    if os.path.isfile(dest):
        try:
            with open(dest, 'r', encoding='utf-8') as handle:
                data = json.load(handle)
        except ValueError as error:
            raise ExportError('%s is not valid JSON (%s); fix it by hand, '
                              'because overwriting it would delete another '
                              "team's entry" % (dest, error))
        if not isinstance(data, dict):
            raise ExportError('%s does not hold a JSON object' % dest)
        keep = [p for p in data.get('plugins') or []
                if entry_name(p) != PLUGIN_NAME]
        data['plugins'] = keep + [entry]
    else:
        data = template
        data['plugins'] = [entry]
    data['plugins'].sort(key=entry_name)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    with open(dest, 'w', encoding='ascii', newline='\n') as handle:
        json.dump(data, handle, indent=2)
        handle.write('\n')
    return dest


def scan_for_host_paths(target, relatives):
    offenders = []
    root = repo_root()
    needles = [root, root.replace('\\', '/')] + list(HOST_PATHS)
    for relative in relatives:
        if not relative.endswith(TEXT_SUFFIXES):
            continue
        with open(os.path.join(target, relative), 'r',
                  encoding='utf-8', errors='replace') as handle:
            text = handle.read()
        lowered = text.lower()
        for needle in needles:
            if needle.lower() in lowered:
                offenders.append('%s contains %s' % (relative, needle))
    return offenders


def unpromoted(target, field, days):
    stale = []
    for book in field:
        try:
            result = subprocess.run(
                ['git', '-C', target, 'log', '-1', '--format=%ad',
                 '--date=short', '--', book.path],
                stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, check=True)
            stamp = result.stdout.decode('ascii').strip()
            if not stamp:
                continue
            age = (datetime.date.today()
                   - datetime.date.fromisoformat(stamp)).days
        except Exception:
            continue
        if age >= days:
            stale.append('  [%d] %s, last touched %s, %d days ago'
                         % (book.id, book.title, stamp, age))
    return stale


def run_export(target, dry_run, days):
    root = repo_root()
    try:
        nrlog = import_nrlog(root)
        verified = load_verified(nrlog, root)
        field, warnings = load_field(nrlog, target)
        errors = check_ids(verified, field)
        if errors:
            raise ExportError('playbook id problems:\n  %s'
                              % '\n  '.join(errors))
        pairs = plan_copies(root, target, verified, field)
        marketplace_template()
    except ExportError as error:
        print('export refused: %s' % error)
        return 2

    for line in warnings:
        print('warning: %s' % line)

    print('%d verified playbook(s), %d field playbook(s)'
          % (len(verified), len(field)))

    manifest_src = os.path.join(PLUGIN_SRC, '.claude-plugin', 'plugin.json')
    with open(manifest_src, 'r', encoding='ascii') as handle:
        manifest = json.load(handle)
    version, note = plugin_version(root, manifest.get('version', '1.0.0'))
    if note:
        print('warning: %s' % note)
    manifest['version'] = version
    plugin, _ = plugin_paths(target)
    manifest_dest = os.path.join(plugin, '.claude-plugin', 'plugin.json')

    if dry_run:
        print('dry run: would write %d file(s) and version %s'
              % (len(pairs) + 2, version))
        for _, dest in sorted(pairs):
            print('  %s' % os.path.relpath(dest, target).replace(os.sep, '/'))
        print('  plugins/%s/.claude-plugin/plugin.json' % PLUGIN_NAME)
        print('  .claude-plugin/marketplace.json (merged, not overwritten)')
        return 0

    if os.path.isdir(plugin):
        shutil.rmtree(plugin)
    for source, dest in pairs:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copyfile(source, dest)
    os.makedirs(os.path.dirname(manifest_dest), exist_ok=True)
    with open(manifest_dest, 'w', encoding='ascii', newline='\n') as handle:
        json.dump(manifest, handle, indent=2, sort_keys=True)
        handle.write('\n')
    try:
        merge_marketplace(target)
    except ExportError as error:
        print('export refused: %s' % error)
        return 2

    relatives = [os.path.relpath(dest, target).replace(os.sep, '/')
                 for _, dest in pairs]
    relatives.append(os.path.relpath(manifest_dest, target)
                     .replace(os.sep, '/'))
    offenders = scan_for_host_paths(target, relatives)
    if offenders:
        print('export wrote the tree, but a host path leaked into it:')
        for line in offenders:
            print('  %s' % line)
        print('Fix the source file and rerun before you push.')
        return 2

    print('wrote %d file(s) to %s at version %s'
          % (len(pairs) + 2, target, version))
    aging = unpromoted(target, field, days)
    if aging:
        print('field playbooks unpromoted for %d days or more:' % days)
        for line in aging:
            print(line)
    print('Review git status in the target and push by hand. '
          'This script does not commit and does not push.')
    return 0


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--target', required=True,
                        help='path to a checkout of agents/claude-skills')
    parser.add_argument('--dry-run', action='store_true')
    parser.add_argument('--unpromoted-days', type=int, default=UNPROMOTED_DAYS)
    args = parser.parse_args(argv)
    if not os.path.isdir(args.target):
        print('no directory at %s' % args.target)
        return 2
    return run_export(args.target, args.dry_run, args.unpromoted_days)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
