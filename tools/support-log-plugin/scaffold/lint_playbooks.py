#!/usr/bin/env python3
# Generated file. Edit tools/support-log-plugin/scaffold/lint_playbooks.py in the
# newrelic-dotnet-agent repository; an edit here is lost at the next export.
"""Lint playbook markdown files.

    python lint_playbooks.py teams/dotnet-agent/playbooks/field

Exits 0 when every file is well formed, 1 otherwise. Standard library only, and
standalone on purpose: it runs in the marketplace repository, where nrlog.py is
a generated copy.
"""

import os
import re
import sys

REQUIRED = ('id', 'title', 'tier', 'scope', 'min_level', 'precedence',
            'keywords')
TIERS = ('verified', 'field')
SCOPES = ('profiler', 'managed', 'both')
LEVELS = ('off', 'error', 'warn', 'info', 'debug', 'finest', 'all')
HEADINGS = ('## Customer fix', '## Next ask')
FIELD_ID_MIN = 100
VERIFIED_ID_MAX = 99
SIGNATURE_MIN = 12
FILENAME_ID = re.compile(r'^(\d+)-')
LIST_ITEM = re.compile(r'^\s+-\s+(.*)$')
SCALAR = re.compile(r'^([a-z_]+):\s*(.*)$')
REGEX_SHAPES = (
    ('^', 'a leading ^'),
    ('$', 'a trailing $'),
    ('.*', '.*'),
    ('.+', '.+'),
    ('\\d', '\\d'),
    ('\\w', '\\w'),
    ('\\s', '\\s'),
    ('(?', '(?'),
    ('[0-9', 'a character class'),
    ('[a-z', 'a character class'),
    ('[A-Z', 'a character class'),
    ('{', 'a { brace'),
    ('}', 'a } brace'),
    ('|', 'a | alternation'),
)


def unquote(value):
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in '"\'':
        return value[1:-1]
    return value


def split_frontmatter(text):
    lines = text.split('\n')
    if not lines or lines[0].strip() != '---':
        return None, text
    for index in range(1, len(lines)):
        if lines[index].strip() == '---':
            return '\n'.join(lines[1:index]), '\n'.join(lines[index + 1:])
    return None, text


def parse_fields(block):
    fields = {}
    key = None
    for raw in block.split('\n'):
        if not raw.strip() or raw.lstrip().startswith('#'):
            continue
        item = LIST_ITEM.match(raw)
        if item and key:
            fields.setdefault(key, [])
            if isinstance(fields[key], list):
                fields[key].append(unquote(item.group(1)))
            continue
        found = SCALAR.match(raw)
        if not found:
            continue
        key = found.group(1)
        value = found.group(2).strip()
        if value.startswith('[') and value.endswith(']'):
            inner = value[1:-1].strip()
            fields[key] = [unquote(p) for p in inner.split(',') if p.strip()]
        elif value == '':
            fields[key] = []
        else:
            fields[key] = unquote(value)
    return fields


def check_signature(value):
    errors = []
    if len(value) < SIGNATURE_MIN:
        errors.append('signature "%s" is too short; %d characters minimum, '
                      'because a short substring matches lines it should not'
                      % (value, SIGNATURE_MIN))
    for token, label in REGEX_SHAPES:
        if token == '^' and not value.startswith('^'):
            continue
        if token == '$' and not value.endswith('$'):
            continue
        if token in ('^', '$') or token in value:
            errors.append('signature "%s" is not literal: it contains %s. '
                          'Copy the substring from the log verbatim; the '
                          'matcher escapes it.' % (value, label))
    return errors


def lint_file(path):
    name = os.path.basename(path)
    with open(path, 'rb') as handle:
        raw = handle.read()
    try:
        text = raw.decode('ascii')
    except UnicodeDecodeError as error:
        return ['%s: not ASCII at byte %d; use - -- " \' and ... instead'
                % (name, error.start)]
    errors = []
    block, body = split_frontmatter(text)
    if block is None:
        return ['%s: no frontmatter; the file must open with a --- line' % name]
    fields = parse_fields(block)
    for key in REQUIRED:
        if key not in fields or fields[key] in ('', [], None):
            errors.append('%s: %s is missing' % (name, key))
    tier = fields.get('tier')
    if tier not in TIERS:
        errors.append('%s: tier is %r, expected one of %s'
                      % (name, tier, ', '.join(TIERS)))
    if fields.get('scope') not in SCOPES:
        errors.append('%s: scope is %r, expected one of %s'
                      % (name, fields.get('scope'), ', '.join(SCOPES)))
    if fields.get('min_level') not in LEVELS:
        errors.append('%s: min_level is %r, expected one of %s'
                      % (name, fields.get('min_level'), ', '.join(LEVELS)))
    ident = None
    try:
        ident = int(fields.get('id'))
    except (TypeError, ValueError):
        errors.append('%s: id is %r, expected an integer'
                      % (name, fields.get('id')))
    try:
        int(fields.get('precedence'))
    except (TypeError, ValueError):
        errors.append('%s: precedence is %r, expected an integer'
                      % (name, fields.get('precedence')))
    if ident is not None:
        if tier == 'field' and ident < FIELD_ID_MIN:
            errors.append('%s: field id %d must be %d or above; 1 to %d are '
                          'verified ids' % (name, ident, FIELD_ID_MIN,
                                            VERIFIED_ID_MAX))
        if tier == 'verified' and ident > VERIFIED_ID_MAX:
            errors.append('%s: verified id %d must be %d or below'
                          % (name, ident, VERIFIED_ID_MAX))
        prefix = FILENAME_ID.match(name)
        if prefix and int(prefix.group(1)) != ident:
            errors.append('%s: filename says id %d, frontmatter says %d'
                          % (name, int(prefix.group(1)), ident))
    if tier == 'field' and not fields.get('observed_in'):
        errors.append('%s: observed_in is required when tier is field; record '
                      'the agent version the log came from' % name)
    if tier == 'verified' and not fields.get('verified_versions'):
        errors.append('%s: verified_versions is required when tier is verified'
                      % name)
    signatures = fields.get('signatures')
    if (not signatures and not fields.get('level_max')
            and fields.get('stated_differs_from_observed') != 'true'):
        errors.append('%s: a playbook needs a non-empty signatures list, a '
                      'level_max, or stated_differs_from_observed: true' % name)
    if isinstance(signatures, list):
        for value in signatures:
            errors.extend('%s: %s' % (name, e) for e in check_signature(value))
    for heading in HEADINGS:
        if heading not in body:
            errors.append('%s: heading %r is missing' % (name, heading))
            continue
        after = body.split(heading, 1)[1]
        chunk = after.split('\n## ', 1)[0].strip()
        if not chunk:
            errors.append('%s: heading %r is empty' % (name, heading))
    return errors


def main(argv):
    targets = argv or [os.path.join('teams', 'dotnet-agent', 'playbooks',
                                    'field')]
    failures = []
    seen = {}
    count = 0
    for target in targets:
        if not os.path.isdir(target):
            print('no directory at %s; nothing to lint' % target)
            continue
        for name in sorted(os.listdir(target)):
            if not name.endswith('.md') or name.lower() == 'readme.md':
                continue
            path = os.path.join(target, name)
            count += 1
            failures.extend(lint_file(path))
            prefix = FILENAME_ID.match(name)
            if prefix:
                ident = int(prefix.group(1))
                if ident in seen:
                    failures.append('duplicate id %d in %s and %s'
                                    % (ident, seen[ident], name))
                else:
                    seen[ident] = name
    if failures:
        print('playbook lint failed, %d problem(s) in %d file(s):'
              % (len(failures), count))
        for line in failures:
            print('  %s' % line)
        return 1
    print('playbook lint passed: %d file(s)' % count)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
