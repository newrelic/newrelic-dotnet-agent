#!/usr/bin/env python3
"""Parse New Relic .NET agent logs and profiler logs for support diagnosis.

Standard library only. Every command streams its input and prints a summary,
so a large log never has to be read whole.
"""

import argparse
import base64
import json
import os
import re
import sys
import tempfile
import time
import urllib.request
import zipfile
from datetime import datetime, timedelta, timezone

MANAGED_RE = re.compile(
    r'^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3}) NewRelic\s+(\S+): '
    r'\[pid: (\d+), tid: (\d+)\] (.*)$'
)
PROFILER_RE = re.compile(r'^\[(\w+)\s*\] (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) (.*)$')

START_RE = re.compile(
    r"The New Relic \.NET Agent v(\S+) started \(pid (\d+)\) on app domain '(.*)'"
)
STOP_RE = re.compile(
    r"The New Relic \.NET Agent v(\S+) has shutdown \(pid (\d+)\) on app domain '(.*)'"
)
LEVEL_RE = re.compile(r'Log level set to (\S+)')
LEVEL_CHANGE_RE = re.compile(r'The log level was updated to (\S+) from (\S+)')
RUNTIME_RE = re.compile(r'\.NET Runtime Version: (.+)$')

REQ_RE = re.compile(r'^Request\(([^)]+)\): (.*)$', re.S)
INVOKING_RE = re.compile(r'^Invoking "([^"]+)"')
INVOKED_RE = re.compile(r'^Invoked "([^"]+)" with : (.*)$', re.S)
YIELDED_RE = re.compile(r'^Invocation of "([^"]+)" yielded response : (.*)$', re.S)
HEADERS_RE = re.compile(r'^Invocation of "([^"]+)" returned response headers : (.*)$', re.S)
RECEIVED_RE = re.compile(
    r'^Received a (\d+) (\S+) response invoking method "([^"]+)" with payload "(.*)"$', re.S
)
ERRORED_RE = re.compile(
    r'^An error occurred invoking method "([^"]+)" with payload "(.*)": (.*)$', re.S
)
DROPPED_RE = re.compile(r'^Dropped large payload: size: (\d+), max_payload_size_bytes=(\d+)')

REDACTIONS = [
    (re.compile(r'(license[_.]?key["\s]*[:=]\s*"?)([^&"\s,}]+)', re.I), r'\1[REDACTED]'),
    (re.compile(r'(LICENSE_KEY\b[^:\n]{0,20}:\s*)(\S+)', re.I), r'\1[REDACTED]'),
    (re.compile(r'\b[0-9a-zA-Z]{36}NRAL\b'), '[REDACTED]'),
    (re.compile(r'\b(NRAK|NRJS|NRII|NRRA|NRBR)-[0-9A-Za-z]{20,}\b'), '[REDACTED]'),
    (re.compile(r'(security[_.]?policies[_.]?token["\s]*[:=]\s*"?)([^&"\s,}]+)', re.I),
     r'\1[REDACTED]'),
    (re.compile(r'(proxy[_.]?(?:user|pass|password)(?:[_.]?obfuscated)?["\s]*[:=]\s*"?)'
                r'([^&"\s,}]+)', re.I), r'\1[REDACTED]'),
    (re.compile(r'(obscuring[_.]?key["\s]*[:=]\s*"?)([^&"\s,}]+)', re.I), r'\1[REDACTED]'),
    (re.compile(r'(Authorization["\s]*[:=]\s*"?)([^"\r\n,}]+)', re.I), r'\1[REDACTED]'),
]

PROFILER_SIGNATURES = [
    ('initialized', 'Profiler initialized'),
    ('process-rejected', 'is not configured to be instrumented'),
    ('unloading', 'should not be instrumented, unloading profiler'),
    ('config-found', 'Found newrelic.config at:'),
    ('config-missing', 'The global newrelic.config file was not found at:'),
    ('extensions-loaded', 'Loading instrumentation from'),
    ('extensions-missing', 'Unable to find the New Relic Agent extensions directory'),
    ('xml-read-failed', 'An exception was thrown while reading instrumentation file:'),
    ('xml-parse-failed', 'Unable to parse one or more instrumentation files'),
    ('rejit-class-missing', 'for rejit. HR:'),
    ('runtime-too-old', 'or greater required. Profiler not attaching.'),
    ('clr-init-failed', 'Error initializing CLR profiler info:'),
    ('live-instrumentation', 'Applying live instrumentation'),
]

# groups that report health, not a fault: no playbook carries their literals,
# because a playbook signature always means a fault is present
PROFILER_HEALTHY_GROUPS = ('initialized', 'config-found', 'extensions-loaded',
                           'live-instrumentation')
# groups that are expected on a .NET Framework host and are not a finding
PROFILER_NOISE_GROUPS = ('process-rejected', 'unloading')

INSTRUMENTING_RE = re.compile(r'^Instrumenting (?:API |helper )?method: (.*)$')

MERGE_GAP = timedelta(minutes=5)

PLAYBOOK_REQUIRED = ('id', 'title', 'tier', 'scope', 'min_level', 'precedence',
                     'signatures', 'keywords')
PLAYBOOK_TIERS = ('verified', 'field')
PLAYBOOK_SCOPES = ('managed', 'profiler', 'both')
PLAYBOOK_TRUE = ('true', 'yes', 'on')
PLAYBOOK_FALSE = ('false', 'no', 'off')
SHIPPED_ID_MAX = 99
REGEX_HINT = re.compile(r'[{}|]|\.\*|\.\+|\\[dws]|\[0-9\]')

LEVEL_ORDER = {'OFF': 0, 'ERROR': 1, 'WARN': 2, 'INFO': 3, 'DEBUG': 4, 'FINEST': 5}
LEVEL_ALIASES = {
    'VERBOSE': 'FINEST', 'FINE': 'FINEST', 'FINER': 'FINEST', 'TRACE': 'FINEST',
    'ALL': 'FINEST', 'NOTICE': 'INFO', 'ALERT': 'WARN', 'CRITICAL': 'ERROR',
    'EMERGENCY': 'ERROR', 'FATAL': 'ERROR', 'SEVERE': 'ERROR', 'AUDIT': 'INFO',
}


def normalize_level(name):
    token = (name or '').strip().rstrip(':').upper()
    token = LEVEL_ALIASES.get(token, token)
    return token if token in LEVEL_ORDER else 'INFO'


def known_level(name):
    """True when the token names a level, directly or through an alias."""
    token = (name or '').strip().rstrip(':').upper()
    return LEVEL_ALIASES.get(token, token) in LEVEL_ORDER


def level_at_least(observed, required):
    return LEVEL_ORDER[normalize_level(observed)] >= LEVEL_ORDER[normalize_level(required)]


def level_at_most(observed, ceiling):
    return LEVEL_ORDER[normalize_level(observed)] <= LEVEL_ORDER[normalize_level(ceiling)]


def looks_like_regex(text):
    """True when a signature carries template or regex syntax instead of literal text."""
    if REGEX_HINT.search(text):
        return True
    return text.startswith('^') or text.endswith('$')


def playbook_flag(value):
    """True for a frontmatter boolean token; False for a false token or an absent one."""
    if value is True or value is False:
        return value
    return isinstance(value, str) and value.strip().lower() in PLAYBOOK_TRUE


def _known_flag(value):
    if value is None or value is True or value is False:
        return True
    return isinstance(value, str) and value.strip().lower() in PLAYBOOK_TRUE + PLAYBOOK_FALSE


def _scalar(raw):
    text = raw.strip()
    if len(text) >= 2 and text[0] == text[-1] and text[0] in '"\'':
        return text[1:-1]
    if re.fullmatch(r'-?\d+', text):
        return int(text)
    return text


def parse_frontmatter(text):
    """Parse the restricted frontmatter dialect used by playbook files; not YAML."""
    lines = text.split('\n')
    if not lines or lines[0].strip() != '---':
        raise ValueError('file does not start with a --- frontmatter block')
    end = None
    for index in range(1, len(lines)):
        if lines[index].strip() == '---':
            end = index
            break
    if end is None:
        raise ValueError('frontmatter block is not terminated by ---')

    fields = {}
    current = None
    for raw in lines[1:end]:
        if not raw.strip() or raw.lstrip().startswith('#'):
            continue
        item = re.match(r'\s+-\s+(.*)$', raw)
        if item and current is not None:
            fields[current].append(_scalar(item.group(1)))
            continue
        pair = re.match(r'([A-Za-z_][A-Za-z0-9_]*):\s*(.*)$', raw)
        if not pair:
            raise ValueError('cannot parse frontmatter line: %r' % raw)
        key, value = pair.group(1), pair.group(2).strip()
        if value == '':
            fields[key] = []
            current = key
        elif value.startswith('[') and value.endswith(']'):
            inner = value[1:-1].strip()
            fields[key] = [_scalar(p) for p in inner.split(',') if p.strip()]
            current = None
        else:
            fields[key] = _scalar(value)
            current = None
    return fields, '\n'.join(lines[end + 1:]).lstrip('\n')


def validate_playbook(fields, path):
    errors = []
    for key in PLAYBOOK_REQUIRED:
        if key not in fields:
            errors.append('%s: missing required field %r' % (path, key))
    if not isinstance(fields.get('id'), int):
        errors.append('%s: id must be an integer' % path)
    if not isinstance(fields.get('precedence'), int):
        errors.append('%s: precedence must be an integer' % path)
    if fields.get('tier') not in PLAYBOOK_TIERS:
        errors.append('%s: tier must be one of %s' % (path, ', '.join(PLAYBOOK_TIERS)))
    if fields.get('scope') not in PLAYBOOK_SCOPES:
        errors.append('%s: scope must be one of %s' % (path, ', '.join(PLAYBOOK_SCOPES)))
    if not known_level(fields.get('min_level')):
        errors.append('%s: min_level %r is not a known level token'
                      % (path, fields.get('min_level')))
    level_max = fields.get('level_max')
    if level_max is not None and not known_level(level_max):
        errors.append('%s: level_max %r is not a known level token' % (path, level_max))
    if not _known_flag(fields.get('stated_differs_from_observed')):
        errors.append('%s: stated_differs_from_observed %r is not a boolean token'
                      % (path, fields.get('stated_differs_from_observed')))
    condition = playbook_flag(fields.get('stated_differs_from_observed'))
    signatures = fields.get('signatures') or []
    if not signatures and not level_max and not condition:
        errors.append('%s: a playbook must carry a non-empty signatures list, '
                      'a level_max, or stated_differs_from_observed' % path)
    for signature in signatures:
        if not isinstance(signature, str) or not signature.strip():
            errors.append('%s: signature %r is not a non-empty string' % (path, signature))
        elif looks_like_regex(signature):
            errors.append('%s: signature %r must be a literal string, not a regex'
                          % (path, signature))
    if fields.get('tier') == 'field' and not fields.get('observed_in'):
        errors.append('%s: a field-tier playbook must set observed_in' % path)
    if fields.get('tier') == 'verified' and not fields.get('verified_versions'):
        errors.append('%s: a verified playbook must set verified_versions' % path)
    book_id = fields.get('id')
    if isinstance(book_id, int):
        if fields.get('tier') == 'verified' and book_id > SHIPPED_ID_MAX:
            errors.append('%s: a verified playbook must have id %d or lower'
                          % (path, SHIPPED_ID_MAX))
        if fields.get('tier') == 'field' and book_id <= SHIPPED_ID_MAX:
            errors.append('%s: a field-tier playbook must have id above %d'
                          % (path, SHIPPED_ID_MAX))
    return errors


class Playbook:
    def __init__(self, fields, body, path):
        self.id = fields['id']
        self.title = fields['title']
        self.tier = fields['tier']
        self.scope = fields['scope']
        self.min_level = normalize_level(fields['min_level'])
        self.level_max = (normalize_level(fields['level_max'])
                          if fields.get('level_max') else None)
        self.stated_differs_from_observed = playbook_flag(
            fields.get('stated_differs_from_observed'))
        self.precedence = fields['precedence']
        self.signatures = list(fields['signatures'])
        self.keywords = list(fields.get('keywords') or [])
        self.verified_versions = fields.get('verified_versions')
        self.observed_in = fields.get('observed_in')
        self.body = body
        self.path = path

    def label(self):
        return '[%d]%s %s' % (self.id, '' if self.tier == 'verified' else ' [field]',
                              self.title)

    def section(self, heading):
        """Return the text under '## heading' up to the next '## ', or None."""
        match = re.search(r'^##\s+%s\s*$(.*?)(?=^##\s|\Z)' % re.escape(heading),
                          self.body, re.M | re.S)
        return match.group(1).strip() if match else None


def default_playbook_dir():
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.normpath(os.path.join(here, '..', 'references', 'playbooks'))


def load_playbooks(directory=None):
    directory = directory or default_playbook_dir()
    if not os.path.isdir(directory):
        sys.exit('playbook directory not found: %s' % directory)
    books = []
    errors = []
    for name in sorted(os.listdir(directory)):
        if not name.endswith('.md') or name.lower() == 'readme.md':
            continue
        path = os.path.join(directory, name)
        with open(path, 'r', encoding='utf-8') as handle:
            text = handle.read()
        try:
            fields, body = parse_frontmatter(text)
        except ValueError as problem:
            errors.append('%s: %s' % (name, problem))
            continue
        found = validate_playbook(fields, name)
        if found:
            errors.extend(found)
            continue
        books.append(Playbook(fields, body, path))
    if errors:
        sys.exit('invalid playbook file(s):\n  ' + '\n  '.join(errors))
    seen = {}
    for book in books:
        if book.id in seen:
            sys.exit('duplicate playbook id %d in %s and %s'
                     % (book.id, seen[book.id], os.path.basename(book.path)))
        seen[book.id] = os.path.basename(book.path)
    return sorted(books, key=lambda b: (b.precedence, b.id))


PROFILER_NAME_RE = re.compile(r'NewRelic\.Profiler\.(\d+)\.log$', re.I)
EVIDENCE_WIDTH = 200

_PROFILER_CACHE = {}


class Match:
    def __init__(self, playbook):
        self.playbook = playbook
        self.state = None
        self.hits = 0
        self.evidence = []
        self.reason = None


def reset_profiler_cache():
    _PROFILER_CACHE.clear()


def read_profiler_cached(path):
    """Parse a profiler log at most once per run; correlation and matching both read it."""
    key = os.path.abspath(path)
    if key not in _PROFILER_CACHE:
        _PROFILER_CACHE[key] = read_profiler(path)
    return _PROFILER_CACHE[key]


def session_observed_level(session):
    """The most verbose level actually present, which bounds every verdict."""
    best = 'OFF'
    for token, count in (session.level_counts or {}).items():
        if count and LEVEL_ORDER[normalize_level(token)] > LEVEL_ORDER[normalize_level(best)]:
            best = normalize_level(token)
    return best


def profiler_logs_for_session(session, directory):
    """Profiler logs whose pid matches and whose UTC range overlaps the session."""
    if not os.path.isdir(directory):
        return []
    found = []
    for name in sorted(os.listdir(directory)):
        match = PROFILER_NAME_RE.search(name)
        if not match or int(match.group(1)) != session.pid:
            continue
        path = os.path.join(directory, name)
        entries = read_profiler_cached(path)
        if not entries:
            continue
        try:
            first = datetime.strptime(entries[0][1], '%Y-%m-%d %H:%M:%S')
            last = datetime.strptime(entries[-1][1], '%Y-%m-%d %H:%M:%S')
        except ValueError:
            continue
        if last >= session.start - MERGE_GAP and first <= session.end + MERGE_GAP:
            found.append(path)
    return found


def _signature_index(playbooks, scopes):
    pairs = []
    for book in playbooks:
        if book.scope in scopes or book.scope == 'both':
            for signature in book.signatures:
                pairs.append((signature, book.id))
    gate = re.compile('|'.join(re.escape(s) for s, _i in pairs)) if pairs else None
    return pairs, gate


def _record_line(results, pairs, name, lineno, text, max_evidence, width):
    """One hit per playbook per line, however many of that playbook's signatures match."""
    counted = set()
    for signature, book_id in pairs:
        if book_id in counted or signature not in text:
            continue
        counted.add(book_id)
        result = results[book_id]
        result.hits += 1
        if len(result.evidence) < max_evidence:
            result.evidence.append((name, lineno, redact(text)[:width]))


def match_playbooks(session, playbooks, profiler_paths=(), max_evidence=2,
                    width=EVIDENCE_WIDTH):
    results = {book.id: Match(book) for book in playbooks}

    managed_pairs, managed_gate = _signature_index(playbooks, ('managed',))
    if managed_gate:
        owner_file = None
        in_session = False
        for path, lineno, ts, _level, pid, _tid, message, raw in iter_entries(session.files):
            if path != owner_file:
                owner_file = path
                in_session = False
            if ts is None:
                text = raw
            else:
                text = message
                in_session = (pid == session.pid
                              and session.start <= ts <= session.end)
            if not in_session:
                continue
            if not managed_gate.search(text):
                continue
            _record_line(results, managed_pairs, os.path.basename(path), lineno, text,
                         max_evidence, width)

    profiler_pairs, profiler_gate = _signature_index(playbooks, ('profiler',))
    if profiler_gate:
        for path in profiler_paths:
            for lineno, (_level, _ts, message) in enumerate(read_profiler_cached(path), 1):
                if not profiler_gate.search(message):
                    continue
                _record_line(results, profiler_pairs, os.path.basename(path), lineno,
                             message, max_evidence, width)

    observed = session_observed_level(session)
    for book in playbooks:
        result = results[book.id]
        if result.hits:
            result.state = 'matched'
            result.reason = 'signature'
        elif book.level_max and level_at_most(observed, book.level_max):
            result.state = 'matched'
            result.reason = 'level'
        elif book.stated_differs_from_observed:
            if not session.log_level:
                result.state = 'blocked'
                result.reason = 'no-stated-level'
            elif (LEVEL_ORDER[observed]
                  > LEVEL_ORDER[normalize_level(session.log_level)]):
                result.state = 'matched'
                result.reason = 'level-change'
            else:
                result.state = 'clear'
        elif book.scope != 'profiler' and not level_at_least(observed, book.min_level):
            result.state = 'blocked'
        else:
            result.state = 'clear'
    return [results[book.id] for book in playbooks]


def redact(text):
    for pattern, replacement in REDACTIONS:
        text = pattern.sub(replacement, text)
    return text


def parse_arg_ts(value):
    for shape in ('%Y-%m-%d %H:%M:%S', '%Y-%m-%d %H:%M', '%Y-%m-%d'):
        try:
            return datetime.strptime(value, shape)
        except ValueError:
            continue
    sys.exit('bad timestamp %r; use "YYYY-MM-DD HH:MM:SS"' % value)


def parse_ts(value):
    return datetime.strptime(value, '%Y-%m-%d %H:%M:%S,%f')


def fmt_ts(value):
    return value.strftime('%Y-%m-%d %H:%M:%S.%f')[:-3]


class Session:
    def __init__(self, pid, start, source):
        self.pid = pid
        self.start = start
        self.end = start
        self.version = None
        self.runtime = None
        self.appdomains = []
        self.log_level = None
        self.level_counts = {}
        self.level_changes = []
        self.lines = 0
        self.files = [source]
        self.starts = 0
        self.stops = 0
        self.interleaved = False
        self.gap_note = None

    @property
    def all_closed(self):
        return self.stops > 0 and self.stops >= self.starts

    def add_appdomain(self, name):
        if name not in self.appdomains:
            self.appdomains.append(name)

    def appdomain_label(self):
        if not self.appdomains:
            return '?'
        if len(self.appdomains) == 1:
            return self.appdomains[0]
        return '%d app domains' % len(self.appdomains)

    def level_label(self):
        if not self.level_counts:
            return 'not stated'
        order = sorted(self.level_counts, key=lambda k: -self.level_counts[k])
        return ','.join('%s:%d' % (k, self.level_counts[k]) for k in order)

    def flags(self):
        out = []
        if self.starts == 0:
            out.append('head-truncated')
        if not self.all_closed:
            out.append('tail-truncated')
        if self.starts > 1:
            out.append('appdomain-ambiguous')
        elif not self.appdomains:
            out.append('appdomain-unknown')
        if self.interleaved:
            out.append('interleaved')
        if self.gap_note:
            out.append('gap-split')
        return out


def is_managed_log(name):
    lower = name.lower()
    return (lower.startswith('newrelic_agent') and lower.endswith('.log')
            and 'audit' not in lower)


def is_profiler_log(name):
    lower = name.lower()
    return lower.startswith('newrelic.profiler.') and lower.endswith('.log')


def first_timestamp(path):
    try:
        with open(path, 'r', encoding='utf-8', errors='replace') as handle:
            for _ in range(500):
                line = handle.readline()
                if not line:
                    break
                match = MANAGED_RE.match(line.rstrip('\n'))
                if match:
                    return parse_ts(match.group(1))
    except OSError:
        pass
    return None


def collect_managed(path, name_filter=None):
    if os.path.isfile(path):
        found = [path]
    elif os.path.isdir(path):
        found = [os.path.join(path, n) for n in sorted(os.listdir(path)) if is_managed_log(n)]
        if not found:
            sys.exit('no newrelic_agent*.log files in %s' % path)
    else:
        sys.exit('not found: %s' % path)
    if name_filter:
        needle = name_filter.lower()
        found = [p for p in found if needle in os.path.basename(p).lower()]
        if not found:
            sys.exit('no managed log matching --file %s' % name_filter)
    return sorted(found, key=lambda p: (first_timestamp(p) or datetime.max, p))


def iter_entries(paths):
    """Yield (path, lineno, timestamp, level, pid, tid, message, raw) per parsed line.

    Continuation lines yield timestamp None and inherit nothing; the caller
    decides how to attach them.
    """
    for path in paths:
        with open(path, 'r', encoding='utf-8', errors='replace') as handle:
            for lineno, raw in enumerate(handle, 1):
                raw = raw.rstrip('\n')
                match = MANAGED_RE.match(raw)
                if match:
                    yield (path, lineno, parse_ts(match.group(1)), match.group(2),
                           int(match.group(3)), int(match.group(4)), match.group(5), raw)
                else:
                    yield (path, lineno, None, None, None, None, None, raw)


def build_sessions(paths):
    open_sessions = {}
    finished = []
    last_file = {}

    def close(pid):
        session = open_sessions.pop(pid, None)
        if session:
            finished.append(session)

    for path, _lineno, ts, _level, pid, _tid, message, _raw in iter_entries(paths):
        if ts is None:
            continue

        start = START_RE.search(message)
        stop = STOP_RE.search(message)
        current = open_sessions.get(pid)

        if current and last_file.get(pid) != path:
            if ts - current.end > MERGE_GAP:
                gap = ts - current.end
                close(pid)
                current = None
                pending_gap = gap
            else:
                pending_gap = None
                if path not in current.files:
                    current.files.append(path)
        else:
            pending_gap = None

        if start:
            if current and current.all_closed:
                close(pid)
                current = None
            if current is None:
                current = Session(pid, ts, path)
                open_sessions[pid] = current
            current.starts += 1
            current.version = current.version or start.group(1)
            current.add_appdomain(start.group(3))
        elif current is None:
            current = Session(pid, ts, path)
            if pending_gap:
                current.gap_note = pending_gap
            open_sessions[pid] = current

        for other_pid, other in open_sessions.items():
            if other_pid != pid:
                other.interleaved = True

        last_file[pid] = path
        current.end = ts
        current.lines += 1
        token = _level.rstrip(':')
        current.level_counts[token] = current.level_counts.get(token, 0) + 1
        change = LEVEL_CHANGE_RE.search(message)
        if change:
            current.level_changes.append((ts, change.group(2), change.group(1)))
        if stop:
            current.stops += 1
            current.version = current.version or stop.group(1)
            current.add_appdomain(stop.group(3))
        level = LEVEL_RE.search(message)
        if level and not current.log_level:
            current.log_level = level.group(1)
        runtime = RUNTIME_RE.search(message)
        if runtime and not current.runtime:
            current.runtime = runtime.group(1).strip()

    for pid in list(open_sessions):
        close(pid)

    finished.sort(key=lambda s: (s.start, s.pid))
    return finished


def print_session_rows(sessions):
    print('%-4s %-8s %-23s %-23s %9s %-10s %s' %
          ('#', 'pid', 'start (UTC)', 'end (UTC)', 'lines', 'version', 'app domain'))
    for index, session in enumerate(sessions, 1):
        print('%-4d %-8d %-23s %-23s %9d %-10s %s' % (
            index, session.pid, fmt_ts(session.start), fmt_ts(session.end),
            session.lines, session.version or '?', session.appdomain_label()))
        detail = ['file=%s' % ','.join(os.path.basename(f) for f in session.files)]
        detail.append('stated=%s' % (session.log_level or '?'))
        detail.append('observed=%s' % session.level_label())
        flags = session.flags()
        if flags:
            detail.append('flags=%s' % ','.join(flags))
        if session.gap_note:
            detail.append('gap-before=%s' % session.gap_note)
        print('     %s' % '  '.join(detail))
        for ts, was, now in session.level_changes[:4]:
            print('     level change: %s %s -> %s' % (fmt_ts(ts), was, now))
        if len(session.level_changes) > 4:
            print('     ... %d more level changes' % (len(session.level_changes) - 4))
        if len(session.appdomains) > 1:
            print('     app domains: %s' % ', '.join(session.appdomains[:12]) +
                  (' ...' if len(session.appdomains) > 12 else ''))


def print_file_summary(sessions, paths):
    print('%-52s %9s %6s %-23s %-23s' %
          ('file', 'sessions', 'pids', 'first (UTC)', 'last (UTC)'))
    by_file = {}
    for session in sessions:
        by_file.setdefault(os.path.basename(session.files[0]), []).append(session)
    for name in sorted(by_file, key=lambda n: -len(by_file[n])):
        group = by_file[name]
        print('%-52s %9d %6d %-23s %-23s' % (
            name[:52], len(group), len({s.pid for s in group}),
            fmt_ts(min(s.start for s in group)), fmt_ts(max(s.end for s in group))))
        levels = {}
        for session in group:
            for token, count in session.level_counts.items():
                levels[token] = levels.get(token, 0) + count
        busiest = max(group, key=lambda s: s.lines)
        changes = sum(len(s.level_changes) for s in group)
        print('     observed=%s  busiest=pid %d with %d lines%s' % (
            ','.join('%s:%d' % (k, levels[k]) for k in sorted(levels, key=lambda k: -levels[k]))
            or 'none', busiest.pid, busiest.lines,
            '  level-changes=%d' % changes if changes else ''))
    print()
    print('%d session(s) across %d file(s). Narrow with --file <name>, '
          'or list every session with --all.' % (len(sessions), len(paths)))


def cmd_sessions(args):
    paths = collect_managed(args.path, args.file)
    sessions = build_sessions(paths)
    if not sessions:
        print('no parseable agent log lines found')
        return
    if len(sessions) > args.limit and not args.all:
        print_file_summary(sessions, paths)
        return
    print_session_rows(sessions)
    print()
    print('%d session(s) across %d file(s)' % (len(sessions), len(paths)))


def pick_session(paths, number):
    sessions = build_sessions(paths)
    if not sessions:
        sys.exit('no parseable agent log lines found')
    if number is None:
        if len(sessions) > 1:
            sys.exit('%d sessions found; pass --session N (run "sessions" first)'
                     % len(sessions))
        return sessions[0]
    if number < 1 or number > len(sessions):
        sys.exit('session %d out of range (1..%d)' % (number, len(sessions)))
    return sessions[number - 1]


def slim_message(message, max_width):
    request = REQ_RE.match(message)
    if request:
        guid, rest = request.group(1), request.group(2)
        invoked = INVOKED_RE.match(rest)
        if invoked:
            return ('Request(%s): Invoked "%s" with : <request body %d bytes; '
                    'nrlog.py body --request %s --direction request>'
                    % (guid, invoked.group(1), len(invoked.group(2)), guid))
        yielded = YIELDED_RE.match(rest)
        if yielded:
            return ('Request(%s): Invocation of "%s" yielded response : '
                    '<response body %d bytes; nrlog.py body --request %s '
                    '--direction response>'
                    % (guid, yielded.group(1), len(yielded.group(2)), guid))
        received = RECEIVED_RE.match(rest)
        if received:
            return ('Request(%s): Received a %s %s response invoking method "%s" '
                    'with payload <%d bytes>'
                    % (guid, received.group(1), received.group(2), received.group(3),
                       len(received.group(4))))
    if len(message) > max_width:
        return '%s ... [+%d chars elided]' % (message[:max_width], len(message) - max_width)
    return message


def out_dir_for(paths, override):
    if override:
        target = override
    else:
        base = os.path.dirname(os.path.abspath(paths[0]))
        target = os.path.join(base, 'nrlog-work')
    os.makedirs(target, exist_ok=True)
    return target


def write_slim(session, target, levels=None, since=None, until=None, needle=None,
               max_width=200):
    """Write one session as a slim redacted file; return the entry count."""
    kept = 0
    with open(target, 'w', encoding='utf-8') as sink:
        sink.write('# nrlog.py slim extract: pid %d, %s .. %s, flags: %s\n'
                   % (session.pid, fmt_ts(session.start), fmt_ts(session.end),
                      ','.join(session.flags()) or 'none'))
        sink.write('# source: %s\n' % ', '.join(os.path.basename(f) for f in session.files))
        sink.write('# payload bodies stripped; secrets redacted\n')
        ours = False
        for _path, _lineno, ts, level, pid, tid, message, raw in iter_entries(session.files):
            if ts is None:
                if ours:
                    sink.write(redact(raw[:max_width]) + '\n')
                continue
            ours = pid == session.pid and session.start <= ts <= session.end
            if ours and levels and level.rstrip(':').upper() not in levels:
                ours = False
            if ours and since and ts < since:
                ours = False
            if ours and until and ts > until:
                ours = False
            if ours and needle and not needle.search(message):
                ours = False
            if not ours:
                continue
            sink.write('%s NewRelic %6s: [pid: %d, tid: %d] %s\n' % (
                ts.strftime('%Y-%m-%d %H:%M:%S,') + '%03d' % (ts.microsecond // 1000),
                level, pid, tid, redact(slim_message(message, max_width))))
            kept += 1
    return kept


def cmd_extract(args):
    paths = collect_managed(args.path, args.file)
    session = pick_session(paths, args.session)
    target_dir = out_dir_for(paths, args.out)
    suffix = ''
    for label, value in (('lvl', args.level), ('since', args.since),
                         ('until', args.until), ('grep', args.grep)):
        if value:
            suffix += '-%s_%s' % (label, re.sub(r'[^A-Za-z0-9]+', '', value)[:20])
    name = 'session-%s-pid%d-%s%s.slim.log' % (
        args.session or 1, session.pid, session.start.strftime('%Y%m%dT%H%M%S'), suffix)
    target = os.path.join(target_dir, name)

    levels = {v.upper() for v in args.level.split(',')} if args.level else None
    since = parse_arg_ts(args.since) if args.since else None
    until = parse_arg_ts(args.until) if args.until else None
    needle = re.compile(args.grep) if args.grep else None

    kept = write_slim(session, target, levels=levels, since=since, until=until,
                      needle=needle, max_width=args.max_width)
    size = os.path.getsize(target)
    print('wrote %s' % target)
    print('%d entries, %.1f MB, pid %d, %s .. %s'
          % (kept, size / 1048576.0, session.pid,
             fmt_ts(session.start), fmt_ts(session.end)))
    if size > 5 * 1048576:
        print('This slim file is still too large to read whole. Narrow it with '
              '--level, --since, --until, or --grep, or grep it for counts first.')
    if session.flags():
        print('flags: %s' % ','.join(session.flags()))
    if session.log_level:
        print('stated log level: %s' % session.log_level)
    else:
        print('stated log level: not stated in this window')
    print('observed levels: %s' % session.level_label())
    for ts, was, now in session.level_changes:
        print('level change: %s %s -> %s' % (fmt_ts(ts), was, now))


def classify_request(rest):
    invoking = INVOKING_RE.match(rest)
    if invoking:
        return ('invoking', invoking.group(1), 0, '')
    invoked = INVOKED_RE.match(rest)
    if invoked:
        return ('request', invoked.group(1), len(invoked.group(2)), '')
    yielded = YIELDED_RE.match(rest)
    if yielded:
        return ('response', yielded.group(1), len(yielded.group(2)), '200')
    headers = HEADERS_RE.match(rest)
    if headers:
        return ('headers', headers.group(1), len(headers.group(2)), '')
    received = RECEIVED_RE.match(rest)
    if received:
        return ('error', received.group(3), len(received.group(4)),
                '%s %s' % (received.group(1), received.group(2)))
    errored = ERRORED_RE.match(rest)
    if errored:
        return ('exception', errored.group(1), len(errored.group(2)), 'exception')
    dropped = DROPPED_RE.match(rest)
    if dropped:
        return ('dropped', '?', int(dropped.group(1)),
                'over %s bytes' % dropped.group(2))
    return None


def cmd_payloads(args):
    paths = collect_managed(args.path, args.file)
    session = pick_session(paths, args.session) if args.session else None
    rows = []
    counts = {}
    for _path, _lineno, ts, _level, pid, _tid, message, _raw in iter_entries(paths):
        if ts is None:
            continue
        if session and (pid != session.pid or not
                        (session.start <= ts <= session.end)):
            continue
        request = REQ_RE.match(message)
        if not request:
            continue
        found = classify_request(request.group(2))
        if not found:
            continue
        kind, endpoint, size, status = found
        if kind == 'invoking' and not args.all:
            continue
        rows.append((ts, request.group(1), endpoint, kind, size, status))
        counts[endpoint] = counts.get(endpoint, 0) + (1 if kind == 'request' else 0)

    if not rows:
        print('no collector calls in scope. At INFO level the agent logs none of them.')
        return
    print('%-23s %-38s %-24s %-9s %9s %s' %
          ('time (UTC)', 'request guid', 'endpoint', 'kind', 'bytes', 'status'))
    for ts, guid, endpoint, kind, size, status in rows[:args.limit]:
        print('%-23s %-38s %-24s %-9s %9d %s' %
              (fmt_ts(ts), guid, endpoint, kind, size, status))
    if len(rows) > args.limit:
        print('... %d more (raise --limit)' % (len(rows) - args.limit))
    print()
    print('requests by endpoint:')
    for endpoint in sorted(counts):
        if counts[endpoint]:
            print('  %-24s %d' % (endpoint, counts[endpoint]))


def cmd_body(args):
    paths = collect_managed(args.path, args.file)
    wanted = {'request': INVOKED_RE, 'response': YIELDED_RE, 'headers': HEADERS_RE}
    pattern = wanted[args.direction]
    for _path, _lineno, ts, _level, _pid, _tid, message, _raw in iter_entries(paths):
        if ts is None:
            continue
        request = REQ_RE.match(message)
        if not request or request.group(1) != args.request:
            continue
        match = pattern.match(request.group(2))
        if not match:
            continue
        body = redact(match.group(2))
        print('# %s %s %s (%d bytes)' % (fmt_ts(ts), args.direction, match.group(1),
                                         len(body)))
        if len(body) > args.max_bytes:
            print('# truncated to %d bytes; raise --max-bytes to see more'
                  % args.max_bytes)
            body = body[:args.max_bytes]
        if args.raw:
            print(body)
            return
        try:
            print(json.dumps(json.loads(body), indent=2)[:args.max_bytes])
        except ValueError:
            print(body)
        return
    sys.exit('no %s body found for request %s' % (args.direction, args.request))


def cmd_decode(args):
    value = args.value if args.value else sys.stdin.read()
    value = value.strip()
    padded = value + '=' * (-len(value) % 4)
    try:
        raw = base64.b64decode(padded)
    except Exception as error:
        sys.exit('not base64: %s' % error)
    if args.key:
        key = args.key.encode('utf-8')
        raw = bytes(b ^ key[i % len(key)] for i, b in enumerate(raw))
    text = raw.decode('utf-8', errors='replace')
    try:
        print(json.dumps(json.loads(text), indent=2))
    except ValueError:
        print(text)


def read_profiler(path):
    entries = []
    with open(path, 'r', encoding='utf-8', errors='replace') as handle:
        for raw in handle:
            match = PROFILER_RE.match(raw.rstrip('\n'))
            if match:
                entries.append((match.group(1).strip(), match.group(2), match.group(3)))
    return entries


def resolve_profiler_path(path):
    if os.path.isfile(path):
        return [path]
    if os.path.isdir(path):
        found = [os.path.join(path, n) for n in sorted(os.listdir(path))
                 if is_profiler_log(n)]
        if found:
            return found
    sys.exit('no NewRelic.Profiler.<pid>.log found at %s' % path)


def signature_relates(needle, signature):
    """True when a roster literal and a playbook signature name the same log line."""
    return needle in signature or signature in needle


def roster_playbook_ids(names, playbooks):
    """Playbook ids whose signatures cover a roster group's literals."""
    lookup = dict(PROFILER_SIGNATURES)
    found = []
    for name in names:
        needle = lookup.get(name)
        if not needle:
            continue
        for book in playbooks:
            if book.id in found:
                continue
            if any(signature_relates(needle, s) for s in book.signatures):
                found.append(book.id)
    return sorted(found)


def profiler_groups(paths):
    """Group profiler logs by the set of known signatures each one carries."""
    groups = {}
    empty = []
    total_instrumented = 0
    for path in paths:
        entries = read_profiler(path)
        if not entries:
            empty.append(path)
            continue
        names = set()
        for _level, _ts, message in entries:
            for name, needle in PROFILER_SIGNATURES:
                if needle in message:
                    names.add(name)
        instrumented = sum(1 for _l, _t, m in entries if INSTRUMENTING_RE.match(m))
        total_instrumented += instrumented
        key = tuple(sorted(names)) or ('no-known-signature',)
        groups.setdefault(key, []).append((path, entries[0][1], entries[-1][1], instrumented))
    return groups, empty, total_instrumented


def profiler_roster(paths, args):
    """Aggregate view for a directory of profiler logs, of which most are noise."""
    groups, empty, total_instrumented = profiler_groups(paths)

    print('%d profiler log(s), %d with no parseable lines' % (len(paths), len(empty)))
    print()
    def rank(key):
        return (0 if 'initialized' in key else 1,
                0 if 'rejit-class-missing' in key or 'xml-read-failed' in key else 1,
                -len(groups[key]))

    for key in sorted(groups, key=rank):
        members = groups[key]
        print('%d file(s): %s' % (len(members), ', '.join(key)))
        interesting = 'initialized' in key
        shown = sorted(members, key=lambda m: -m[3])[:args.limit if interesting else 3]
        for path, first, last, instrumented in shown:
            print('   %-34s %s .. %s  instrumented=%d'
                  % (os.path.basename(path), first, last, instrumented))
        if len(members) > len(shown):
            print('   ... %d more' % (len(members) - len(shown)))
    print()
    print('total instrumented-method lines across all files: %d' % total_instrumented)
    print('Pass a single NewRelic.Profiler.<pid>.log for the full per-file view.')


def cmd_profiler(args):
    paths = resolve_profiler_path(args.path)
    if len(paths) > args.roster_above:
        profiler_roster(paths, args)
        return
    for path in paths:
        entries = read_profiler(path)
        print('== %s' % os.path.basename(path))
        if not entries:
            print('   no parseable profiler lines. The file exists but the profiler '
                  'wrote nothing readable.')
            continue
        pid_match = re.search(r'NewRelic\.Profiler\.(\d+)\.log', os.path.basename(path))
        print('   pid %s, %s .. %s, %d lines'
              % (pid_match.group(1) if pid_match else '?', entries[0][1],
                 entries[-1][1], len(entries)))
        levels = {}
        for level, _ts, _msg in entries:
            levels[level] = levels.get(level, 0) + 1
        print('   levels: %s' % ', '.join('%s=%d' % (k, levels[k]) for k in sorted(levels)))

        hits = {}
        for _level, ts, message in entries:
            for name, needle in PROFILER_SIGNATURES:
                if needle in message:
                    hits.setdefault(name, []).append((ts, message))
        for name, _needle in PROFILER_SIGNATURES:
            found = hits.get(name)
            if not found:
                continue
            print('   [%s] x%d' % (name, len(found)))
            for ts, message in found[:args.limit]:
                print('      %s %s' % (ts, message.strip()))
            if len(found) > args.limit:
                print('      ... %d more' % (len(found) - args.limit))
        if 'initialized' not in hits:
            print('   [initialized] MISSING - the profiler did not finish loading')

        instrumented = {m for _l, _t, m in entries if INSTRUMENTING_RE.match(m)}
        print('   instrumented methods: %d (see "instrumented" command)' % len(instrumented))
        problems = [(l, t, m) for l, t, m in entries if l in ('Error', 'Warn')]
        if problems:
            print('   warnings and errors: %d' % len(problems))
            for level, ts, message in problems[:args.limit]:
                print('      [%s] %s %s' % (level, ts, message.strip()))
            if len(problems) > args.limit:
                print('      ... %d more' % (len(problems) - args.limit))


def cmd_instrumented(args):
    pattern = re.compile(args.filter) if args.filter else None
    for path in resolve_profiler_path(args.path):
        names = set()
        for _level, _ts, message in read_profiler(path):
            match = INSTRUMENTING_RE.match(message)
            if match:
                name = match.group(1).strip()
                if pattern is None or pattern.search(name):
                    names.add(name)
        print('== %s: %d method(s)%s' % (os.path.basename(path), len(names),
                                         ' matching filter' if pattern else ''))
        for name in sorted(names)[:args.limit]:
            print('   %s' % name)
        if len(names) > args.limit:
            print('   ... %d more (raise --limit)' % (len(names) - args.limit))


REPORT_LABEL_WIDTH = 9
REPORT_TITLE_WIDTH = 52
REPORT_GROUP_WIDTH = 46
REPORT_FILE_CAP = 10
REPORT_GROUP_NAME_CAP = 2
REPORT_GROUP_PID_CAP = 3
CLEAR_ENUMERATE_MAX = 12
REPORT_ERROR_CAP = 5
VERSION_RE = re.compile(r'v?(\d+)\.(\d+)\.(\d+)')


def _pad(label, first=''):
    return '%-*s %s' % (REPORT_LABEL_WIDTH, label, first)


def parse_version(text):
    match = VERSION_RE.match((text or '').strip())
    return tuple(int(g) for g in match.groups()) if match else None


CHANGELOG_URL = ('https://raw.githubusercontent.com/newrelic/newrelic-dotnet-agent/'
                 'main/src/Agent/CHANGELOG.md')
CHANGELOG_TTL_SECONDS = 24 * 60 * 60
CHANGELOG_TIMEOUT_SECONDS = 5

# two release-heading shapes: "## [10.54.0](compare link) (2026-08-25)" from
# release-please, and "## [10.9.0] - 2023-03-28" from the hand-written era
CHANGELOG_HEADING = re.compile(r'^##\s+\[?(\d+\.\d+(?:\.\d+)?)\]?'
                               r'(?:.*?\((\d{4}-\d{2}-\d{2})\)|\s*-\s*(\d{4}-\d{2}-\d{2}))\s*$')
CHANGELOG_SECTION = re.compile(r'^###\s+(.*)$')
CHANGELOG_ITEM = re.compile(r'^\*\s+(.*)$')
CHANGELOG_LINK = re.compile(r'\[([^\]]*)\]\([^)]*\)')
CHANGELOG_ISSUE = re.compile(r'#(\d+)')
CHANGELOG_TRAILER = re.compile(r'(\s*\((?:#\d+|[0-9a-f]{6,12})\))+$')
CHANGELOG_KEPT_SECTIONS = ('fixes', 'bug fixes', 'new features')


def clean(text):
    """One fix title: links flattened, trailing issue and commit refs replaced by one."""
    numbers = CHANGELOG_ISSUE.findall(text)
    text = CHANGELOG_LINK.sub(r'\1', text)
    text = CHANGELOG_TRAILER.sub('', text)
    text = re.sub(r'\s*\(\s*\)\s*', ' ', text)
    text = re.sub(r'\s+', ' ', text).strip(' .')
    if not numbers:
        return text
    tag = '#%s' % numbers[0]
    if re.search(r'(?<!\d)%s(?!\d)' % re.escape(tag), text):
        return text
    return '%s (%s)' % (text, tag)


def parse_changelog(text):
    """The release list: version, date, and kept fix titles, newest release first."""
    releases = []
    current = None
    section = None
    for line in text.splitlines():
        heading = CHANGELOG_HEADING.match(line)
        if heading:
            current = {'version': heading.group(1),
                       'date': heading.group(2) or heading.group(3),
                       'fixes': []}
            releases.append(current)
            section = None
            continue
        if current is None:
            continue
        found = CHANGELOG_SECTION.match(line)
        if found:
            section = found.group(1).strip().lower()
            continue
        item = CHANGELOG_ITEM.match(line)
        if item and section in CHANGELOG_KEPT_SECTIONS:
            current['fixes'].append(clean(item.group(1)))
    return releases


def local_changelog_path():
    """The checkout copy's path, when this script is running from inside the repo."""
    here = os.path.dirname(os.path.abspath(__file__))
    path = os.path.normpath(os.path.join(here, '..', '..', '..', '..',
                                         'src', 'Agent', 'CHANGELOG.md'))
    return path if os.path.isfile(path) else None


def changelog_cache_path():
    """Never inside the skill directory: a plugin install may be read-only and is
    replaced on update."""
    if sys.platform == 'win32':
        base = os.environ.get('LOCALAPPDATA') or os.path.expanduser('~')
    else:
        base = os.environ.get('XDG_CACHE_HOME') or os.path.join(os.path.expanduser('~'),
                                                                 '.cache')
    return os.path.join(base, 'nrlog', 'changelog.md')


def fetch_changelog(url=CHANGELOG_URL, timeout=CHANGELOG_TIMEOUT_SECONDS):
    """The only function in this script that may touch the network. Text, or None."""
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return response.read().decode('utf-8', errors='replace')
    except Exception:
        return None


def resolve_changelog(override=None, fetcher=None, now=None):
    """(text_or_None, source_label), most authoritative source first.

    override -> repo checkout -> fresh cache -> network fetch -> stale cache -> nothing.
    """
    fetcher = fetcher or fetch_changelog
    now = time.time() if now is None else now
    local = local_changelog_path()

    if override:
        # a named source that cannot be read is a hard error, not a fall-through to an implicit one
        try:
            with open(override, 'r', encoding='utf-8', errors='replace') as handle:
                text = handle.read()
        except OSError:
            sys.exit('cannot read --changelog override: %s' % override)
        same_as_checkout = local and os.path.abspath(override) == os.path.abspath(local)
        return text, ('repo checkout' if same_as_checkout else override)

    if local:
        try:
            with open(local, 'r', encoding='utf-8', errors='replace') as handle:
                return handle.read(), 'repo checkout'
        except OSError:
            pass

    cache = changelog_cache_path()
    cached_text = None
    cached_mtime = None
    try:
        cached_mtime = os.path.getmtime(cache)
        with open(cache, 'r', encoding='utf-8', errors='replace') as handle:
            cached_text = handle.read()
        age_seconds = now - cached_mtime
        if age_seconds < CHANGELOG_TTL_SECONDS:
            return cached_text, ('cache from %s'
                                 % datetime.fromtimestamp(cached_mtime, timezone.utc)
                                   .strftime('%Y-%m-%d'))
    except OSError:
        pass

    fetched = fetcher()
    if fetched is not None:
        # write to a temp file and os.replace onto cache, so a concurrent reader never sees a partial write
        tmp_path = None
        try:
            cache_dir = os.path.dirname(cache)
            os.makedirs(cache_dir, exist_ok=True)
            fd, tmp_path = tempfile.mkstemp(dir=cache_dir, prefix='.changelog-', suffix='.tmp')
            with os.fdopen(fd, 'w', encoding='utf-8', newline='\n') as handle:
                handle.write(fetched)
            os.replace(tmp_path, cache)
            tmp_path = None
        except OSError:
            if tmp_path is not None:
                try:
                    os.remove(tmp_path)
                except OSError:
                    pass
        return fetched, ('github, fetched %s'
                         % datetime.fromtimestamp(now, timezone.utc).strftime('%Y-%m-%d'))

    if cached_text is not None:
        age_days = int((now - cached_mtime) // 86400)
        return cached_text, ('cache from %s, %d day(s) old'
                             % (datetime.fromtimestamp(cached_mtime, timezone.utc)
                                .strftime('%Y-%m-%d'), age_days))

    return None, None


def load_versions(override=None, fetcher=None, now=None):
    """The one entry point the report calls: latest version, release list, source
    label, resolved at analysis time rather than published. None when unresolvable."""
    text, source_label = resolve_changelog(override=override, fetcher=fetcher, now=now)
    if text is None:
        return None
    releases = parse_changelog(text)
    if not releases:
        return None
    return {'latest': releases[0]['version'], 'releases': releases,
            'source_label': source_label}


def _keyword_pattern(keyword):
    """A keyword matches a whole word, plural allowed, so `connect` misses `reconnect`."""
    return re.compile(r'(?<!\w)%s(?:s|es)?(?!\w)' % re.escape(keyword))


WORKED_ON_CAP = 20
LOG_TERM_MIN_LENGTH = 4
LOG_TERM_CAP = 8
# framework-ubiquitous names that would match almost any fix and tell the engineer nothing
LOG_TERM_STOPLIST = frozenset(s.lower() for s in (
    'System', 'Microsoft', 'Threading', 'Tasks', 'Collections', 'Generic',
    'String', 'Object', 'Int32', 'Int64', 'Boolean', 'Void', 'Async', 'Task',
    'Method', 'Transaction', 'Wrapper'))
_LOG_TERM_TOKEN_RE = re.compile(r'[A-Za-z][A-Za-z0-9]*')


def extract_log_terms(evidence, cap=LOG_TERM_CAP):
    """Candidate terms pulled from the log's own text, not a playbook's vocabulary.

    evidence is the (name, lineno, text) tuples a matched playbook quoted; the text is
    already redacted and width-capped, so no secret can reach a term. A dotted name like
    `Confluent.Kafka.Consumer` becomes three terms; short and stoplisted ones are dropped.
    """
    seen = set()
    terms = []
    for _name, _lineno, text in evidence:
        for token in _LOG_TERM_TOKEN_RE.findall(text or ''):
            if len(token) < LOG_TERM_MIN_LENGTH:
                continue
            lowered = token.lower()
            if lowered in LOG_TERM_STOPLIST or lowered in seen:
                continue
            seen.add(lowered)
            terms.append(token)
            if len(terms) >= cap:
                return terms
    return terms


def _log_terms_block(log_terms, newer, limit):
    """A filter kept separate from the keyword section: provenance matters here, so an
    engineer can see which hits came from a playbook's vocabulary and which from the log's
    own text."""
    if not log_terms:
        return []
    needles = [_keyword_pattern(t.lower()) for t in log_terms]
    hits = []
    for release in newer:
        for fix in release.get('fixes') or []:
            lowered = fix.lower()
            if any(needle.search(lowered) for needle in needles):
                hits.append((release.get('version', '?'), fix))
    lines = [_pad('LOG-TERMS', 'terms pulled from the log, not a playbook: %s'
                              % ', '.join(log_terms))]
    lines.append(_pad('', 'fixes since, matching those terms: %d' % len(hits)))
    for release_version, fix in hits[:limit]:
        lines.append(_pad('', '  %-9s %s' % (release_version, fix)))
    if len(hits) > limit:
        lines.append(_pad('', '  ... %d more not shown, and any one of them could '
                              'outrank what is shown above' % (len(hits) - limit)))
    if hits:
        lines.append(_pad('', 'a log-term match is not a diagnosis; it is a candidate, '
                              'the same as a keyword match'))
    return lines


def _worked_on_block(worked_on, found, version, releases):
    """Every changelog entry between a customer-named working version and this session's
    version - complete and unfiltered, because that is the commonest regression shape:
    something changed in that window, and a keyword guess should not be needed to find it.
    """
    if worked_on is None:
        return []
    parsed = parse_version(worked_on)
    if not parsed:
        return [_pad('WORKED-ON', '--worked-on "%s" is not a parseable version; '
                                  'skipping the window' % worked_on)]
    if parsed >= found:
        return [_pad('WORKED-ON', '--worked-on %s is not older than the session version '
                                  '%s; no window to show' % (worked_on, version))]
    window = [(release.get('version', '?'), fix)
             for release in releases
             if parsed < (parse_version(release.get('version')) or (0, 0, 0)) <= found
             for fix in (release.get('fixes') or [])]
    lines = [_pad('WORKED-ON', 'window %s .. %s: %d entrie(s), complete and unfiltered'
                              % (worked_on, version, len(window)))]
    for release_version, fix in window[:WORKED_ON_CAP]:
        lines.append(_pad('', '  %-9s %s' % (release_version, fix)))
    if len(window) > WORKED_ON_CAP:
        lines.append(_pad('', '  ... %d more not shown, out of a window meant to be '
                              'complete' % (len(window) - WORKED_ON_CAP)))
    return lines


def version_block(version, keywords, data, limit=4, worked_on=None, log_terms=None):
    """Report the session's currency, the fixes since it that match the symptom, the
    complete window since a version the customer named as working, and any changelog
    hits the log's own vocabulary turns up that the playbook keywords missed."""
    if not data:
        return [_pad('VERSION', 'changelog not reachable; latest version unknown, '
                                'currency not checked')]
    latest = data.get('latest', '?')
    source = data.get('source_label', '?')
    found = parse_version(version)
    if not found:
        return [_pad('VERSION', 'agent version not stated in this session; latest is '
                                '%s (changelog from %s)' % (latest, source))]

    releases = data.get('releases') or []
    newer = [r for r in releases
             if (parse_version(r.get('version')) or (0, 0, 0)) > found]
    if not newer:
        lines = [_pad('VERSION', '%s is current as of the changelog from %s (latest %s)'
                                 % (version, source, latest))]
        lines.extend(_worked_on_block(worked_on, found, version, releases))
        return lines
    lines = [_pad('VERSION', '%s is %d release(s) behind %s (%s), changelog from %s'
                             % (version, len(newer), latest,
                                newer[0].get('date', '?'), source))]
    all_fixes = [(release.get('version', '?'), fix)
                 for release in newer for fix in (release.get('fixes') or [])]
    lines.append(_pad('', '%d fix(es) total across those releases, before any filter'
                      % len(all_fixes)))
    if not keywords:
        lines.append(_pad('', 'no playbook matched, so no fix filter was applied; '
                              'showing the %d most recent fix(es) instead'
                              % min(limit, len(all_fixes))))
        for release_version, fix in all_fixes[:limit]:
            lines.append(_pad('', '  %-9s %s' % (release_version, fix)))
        if len(all_fixes) > limit:
            lines.append(_pad('', '  ... %d more not shown, and any one of them could '
                                  'outrank what is shown above'
                                  % (len(all_fixes) - limit)))
        lines.append(_pad('', 'these are candidates, not findings; nothing has been '
                              'ruled in or ruled out'))
        lines.extend(_log_terms_block(log_terms, newer, limit))
        lines.extend(_worked_on_block(worked_on, found, version, releases))
        return lines

    needles = [_keyword_pattern(k.lower()) for k in keywords]
    hits = []
    for release in newer:
        for fix in release.get('fixes') or []:
            lowered = fix.lower()
            if any(needle.search(lowered) for needle in needles):
                hits.append((release.get('version', '?'), fix))
    lines.append(_pad('', 'fixes since, matching this symptom: %d' % len(hits)))
    for release_version, fix in hits[:limit]:
        lines.append(_pad('', '  %-9s %s' % (release_version, fix)))
    if len(hits) > limit:
        lines.append(_pad('', '  ... %d more not shown, and any one of them could '
                              'outrank what is shown above' % (len(hits) - limit)))
    if hits:
        lines.append(_pad('', 'a keyword match is not a diagnosis; it is a candidate'))
    lines.append(_pad('', 'this filter uses only the matched playbooks\' keywords; '
                          'a wrong match narrows it wrongly'))
    lines.extend(_log_terms_block(log_terms, newer, limit))
    lines.extend(_worked_on_block(worked_on, found, version, releases))
    return lines


def _render_header(playbooks):
    """The header stamps the PLAYBOOKS from their own provenance; the VERSION block
    (if printed) stamps the changelog separately, since the two are unrelated data sets."""
    verified = [parse_version(b.verified_versions) for b in playbooks if b.verified_versions]
    verified = [v for v in verified if v]
    stamp = ('%d.%d.%d' % max(verified)) if verified else 'unknown'
    return ['nrlog triage   playbooks: %d (%d field)   playbooks verified against %s'
            % (len(playbooks), sum(1 for b in playbooks if b.tier == 'field'), stamp),
            '']


def _render_input(directory, managed, profiler_all):
    return [_pad('INPUT', '%s   %d managed log(s), %d profiler log(s)'
                 % (directory, len(managed), len(profiler_all)))]


def _render_choose(managed):
    lines = [_pad('CHOOSE', '%d managed logs in scope. Pass --file <name> to '
                            'pick one application:' % len(managed))]
    for name in [os.path.basename(p) for p in managed][:REPORT_FILE_CAP]:
        lines.append(_pad('', '  %s' % name))
    if len(managed) > REPORT_FILE_CAP:
        lines.append(_pad('', '  ... %d more' % (len(managed) - REPORT_FILE_CAP)))
    lines.append(_pad('NEXT', 'rerun: nrlog.py triage <path> --file <name>'))
    return lines


def _render_session(session, index, total, observed):
    lines = [_pad('SESSION', 'session %d of %d   pid %d   %s .. %s   %d lines'
                  % (index + 1, total, session.pid, fmt_ts(session.start),
                     fmt_ts(session.end), session.lines)),
             _pad('', 'agent %s   app domain %s   flags: %s'
                  % (session.version or '?', session.appdomain_label(),
                     ','.join(session.flags()) or 'none')),
             _pad('', 'level stated=%s  observed %s  most verbose=%s'
                  % (session.log_level or 'not stated', session.level_label(), observed))]
    for when, was, now in session.level_changes:
        lines.append(_pad('', 'level change %s %s -> %s' % (fmt_ts(when), was, now)))
    if total > 1:
        others = ', '.join(str(i + 1) for i in range(total) if i != index)
        lines.append(_pad('', 'other sessions: %s   rerun with --session N' % others))
    return lines


def _group_label(names):
    faults = [n for n in names if n not in PROFILER_HEALTHY_GROUPS]
    shown = faults or list(names)
    text = ', '.join(shown[:REPORT_GROUP_NAME_CAP])
    if len(shown) > REPORT_GROUP_NAME_CAP:
        text += ' +%d' % (len(shown) - REPORT_GROUP_NAME_CAP)
    return text


def _group_pids(members):
    pids = []
    for path, _first, _last, _instrumented in members:
        match = PROFILER_NAME_RE.search(os.path.basename(path))
        if match:
            pids.append(match.group(1))
    if not pids:
        return ''
    if len(pids) > REPORT_GROUP_PID_CAP:
        return 'pids %s, +%d' % (', '.join(pids[:REPORT_GROUP_PID_CAP]),
                                 len(pids) - REPORT_GROUP_PID_CAP)
    return 'pid%s %s' % ('' if len(pids) == 1 else 's', ', '.join(pids))


def _render_profiler(profiler_all, correlated, session, playbooks):
    groups, empty, _instrumented = profiler_groups(profiler_all)
    lines = [_pad('PROFILER', '%d file(s), %d group(s), %d correlated to pid %d '
                              'by time overlap'
                  % (len(profiler_all), len(groups), len(correlated), session.pid))]
    for key in sorted(groups, key=lambda k: (-len(groups[k]), k)):
        notes = []
        if all(name in PROFILER_NOISE_GROUPS for name in key):
            notes.append('expected noise')
        elif all(name in PROFILER_HEALTHY_GROUPS for name in key):
            notes.append('healthy startup')
        ids = roster_playbook_ids(key, playbooks)
        if ids:
            notes.append('playbook %s' % ', '.join(str(i) for i in ids))
        notes.append(_group_pids(groups[key]))
        lines.append(_pad('', '%6d  %-*s %s'
                          % (len(groups[key]), REPORT_GROUP_WIDTH, _group_label(key),
                             '; '.join(n for n in notes if n))))
    if empty:
        lines.append(_pad('', '%d file(s) hold no parseable profiler lines' % len(empty)))
    if profiler_all and not correlated:
        lines.append(_pad('', 'no profiler log matches this pid and time range; '
                              'not pairing them'))
    return lines


def _match_detail(result, session, observed):
    """The lines under one MATCHED label, which differ per match reason."""
    if result.reason == 'level':
        return ['observed level %s is at or below the %s ceiling'
                % (observed, result.playbook.level_max.lower())]
    if result.reason == 'level-change':
        detail = ['observed %s is more verbose than the stated %s'
                  % (observed, normalize_level(session.log_level))]
        for when, was, now in session.level_changes:
            detail.append('level change %s %s -> %s' % (fmt_ts(when), was, now))
        return detail
    return ['%s:%d  %s' % (name, lineno, text) for name, lineno, text in result.evidence]


def _render_matched(selected, total, loaded, session, observed):
    if not selected:
        return [_pad('MATCHED', 'none')]
    lines = [_pad('MATCHED', '%d of %d playbook(s)' % (total, loaded))]
    for result in selected:
        right = ('%d hit(s)' % result.hits if result.reason == 'signature'
                 else 'level condition')
        lines.append(_pad('', '%-*s %s' % (REPORT_TITLE_WIDTH, result.playbook.label(),
                                           right)))
        for detail in _match_detail(result, session, observed):
            lines.append(_pad('', '   %s' % detail))
    if total > len(selected):
        lines.append(_pad('', '... %d more matched, not shown' % (total - len(selected))))
    lines.append(_pad('', 'a signature hit is a candidate, not a conclusion; it still '
                          'has to explain the reported symptom'))
    return lines


def _render_blocked(blocked, observed):
    lines = []
    for result in blocked:
        if result.reason == 'no-stated-level':
            why = 'no startup banner, so the stated level is unknown'
        else:
            why = 'needs %s; this session is %s' % (result.playbook.min_level, observed)
        lines.append(_pad('BLOCKED', '%-*s %s'
                          % (REPORT_TITLE_WIDTH, result.playbook.label(), why)))
    return lines


def _render_clear(clear, loaded):
    if loaded > CLEAR_ENUMERATE_MAX:
        return [_pad('CLEAR', '%d playbook(s) did not match' % len(clear))]
    ids = ', '.join(str(r.playbook.id) for r in sorted(clear, key=lambda r: r.playbook.id))
    return [_pad('CLEAR', 'playbooks %s did not match' % ids if ids else 'none')]


def _render_errors(session, matched, width):
    """ERROR and WARN lines a matched playbook did not already quote as evidence.

    This block is playbook-independent by design: it works on the tickets no
    playbook covers, which is where the tool is weakest.
    """
    quoted = {(name, lineno) for result in matched for name, lineno, _text in result.evidence}
    order = []
    groups = {}
    for path, lineno, ts, level, pid, _tid, message, _raw in iter_entries(session.files):
        if ts is None or pid != session.pid or not (session.start <= ts <= session.end):
            continue
        token = normalize_level(level)
        if token not in ('ERROR', 'WARN'):
            continue
        name = os.path.basename(path)
        if (name, lineno) in quoted:
            continue
        shape = normalize_shape(message)
        if shape not in groups:
            groups[shape] = {'level': token, 'name': name, 'lineno': lineno,
                             'text': message, 'count': 0}
            order.append(shape)
        groups[shape]['count'] += 1

    if not groups:
        return [_pad('ERRORS', 'no ERROR or WARN lines in this session; that is not '
                               'proof of health, only that none were logged')]

    lines = [_pad('ERRORS', '%d group(s) of ERROR/WARN not explained by a matched '
                            'playbook' % len(groups))]
    for shape in order[:REPORT_ERROR_CAP]:
        group = groups[shape]
        lines.append(_pad('', '%-5s %s:%d  %s'
                          % (group['level'], group['name'], group['lineno'],
                             redact(group['text'])[:width])))
        if group['count'] > 1:
            lines.append(_pad('', '   seen %d time(s)' % group['count']))
    if len(groups) > REPORT_ERROR_CAP:
        lines.append(_pad('', '... %d more group(s) not shown' % (len(groups) - REPORT_ERROR_CAP)))
    return lines


def _render_next(selected, blocked, path, index):
    if selected:
        names = ', '.join(os.path.basename(r.playbook.path) for r in selected)
        lines = [_pad('NEXT', 'read references/playbooks/%s' % names)]
    elif blocked:
        lines = [_pad('NEXT', 'nothing matched at this level. Ask the customer to '
                              'reproduce at debug or finest, then rerun.')]
    else:
        lines = [_pad('NEXT', 'nothing matched. State the limit, give the next ask, '
                              'and build the escalation packet.')]
    lines.append(_pad('', 'slim file: nrlog.py slim %s --session %d' % (path, index + 1)))
    return lines


def triage_report(path, file=None, session=None, playbooks=None, max_matched=5,
                  evidence=2, width=EVIDENCE_WIDTH, no_version=False, changelog=None,
                  worked_on=None):
    """One routed verdict for one session: input, level, profiler, playbooks, version."""
    reset_profiler_cache()
    books = load_playbooks(playbooks)
    versions = load_versions(override=changelog)
    directory = path if os.path.isdir(path) else os.path.dirname(os.path.abspath(path))
    managed = collect_managed(path, file)
    profiler_all = [os.path.join(directory, n) for n in sorted(os.listdir(directory))
                    if is_profiler_log(n)] if os.path.isdir(directory) else []

    lines = _render_header(books)
    lines.extend(_render_input(directory, managed, profiler_all))
    if len(managed) > 1 and not file:
        return lines + _render_choose(managed)

    sessions = build_sessions(managed)
    if not sessions:
        return lines + [_pad('SESSION', 'no parseable agent log lines found'),
                        _pad('NEXT', 'check the profiler logs: nrlog.py profiler %s' % path)]

    order = sorted(range(len(sessions)), key=lambda i: sessions[i].start, reverse=True)
    index = (session - 1) if session is not None else order[0]
    if index < 0 or index >= len(sessions):
        sys.exit('session %d out of range (1..%d)' % (session, len(sessions)))
    chosen = sessions[index]
    observed = session_observed_level(chosen)
    correlated = profiler_logs_for_session(chosen, directory)

    results = match_playbooks(chosen, books, correlated, max_evidence=evidence, width=width)
    matched = [r for r in results if r.state == 'matched']
    blocked = [r for r in results if r.state == 'blocked']
    clear = [r for r in results if r.state == 'clear']
    selected = sorted(matched, key=lambda r: (0 if r.playbook.tier == 'verified' else 1,
                                              -r.hits))[:max_matched]
    selected.sort(key=lambda r: (r.playbook.precedence, r.playbook.id))

    lines.extend(_render_session(chosen, index, len(sessions), observed))
    lines.extend(_render_profiler(profiler_all, correlated, chosen, books))
    lines.extend(_render_matched(selected, len(matched), len(books), chosen, observed))
    lines.extend(_render_blocked(blocked, observed))
    lines.extend(_render_clear(clear, len(books)))
    lines.extend(_render_errors(chosen, matched, width))
    if not no_version:
        keywords = []
        selected_evidence = []
        for result in selected:
            keywords.extend(result.playbook.keywords)
            selected_evidence.extend(result.evidence)
        lines.extend(version_block(chosen.version, keywords, versions,
                                   worked_on=worked_on,
                                   log_terms=extract_log_terms(selected_evidence)))
    lines.extend(_render_next(selected, blocked, path, index))
    return lines


def cmd_triage(args):
    for line in triage_report(path=args.path, file=args.file, session=args.session,
                              playbooks=args.playbooks, max_matched=args.max_matched,
                              evidence=args.evidence, width=args.width,
                              no_version=args.no_version, changelog=args.changelog,
                              worked_on=args.worked_on):
        print(line)


ESCALATION_WARNING = (
    'This packet contains host names, application names, SQL text, and request '
    'parameters, because the playbooks need them. Send it to the internal '
    'escalation, never in a customer-facing reply.')

# the packet keeps the levels that carry a verdict and drops the volume below them
ESCALATION_LEVELS = ('ERROR', 'WARN', 'INFO')


def customer_block(playbook):
    fix = playbook.section('Customer fix')
    ask = playbook.section('Next ask')
    lines = []
    if playbook.tier == 'field':
        lines.append('[for you, not the customer] This fix is field-observed, '
                     'not source-verified. Decide before you send it.')
        lines.append('')
    lines.append('Playbook %d: %s' % (playbook.id, playbook.title))
    lines.append('')
    lines.append(fix or 'This playbook records no customer fix.')
    lines.append('')
    lines.append('Next ask: %s' % (ask or 'none recorded.'))
    return lines


def escalation_profiler_name(source):
    """profiler-<pid>.log, or the original basename when the pid is not in the name."""
    base = os.path.basename(source)
    found = PROFILER_NAME_RE.search(base)
    if not found:
        return base
    return 'profiler-%s.log' % found.group(1)


def build_escalation(path, file, session, ticket, out, make_zip, playbooks,
                     changelog=None):
    managed = collect_managed(path, file)
    chosen = pick_session(managed, session)
    directory = path if os.path.isdir(path) else os.path.dirname(os.path.abspath(path))
    label = ticket or 'pid%d-%s' % (chosen.pid, chosen.start.strftime('%Y%m%d'))
    target = os.path.join(out_dir_for(managed, out), 'escalation-%s' % label)
    os.makedirs(target, exist_ok=True)
    written = []

    report = os.path.join(target, 'triage-report.txt')
    with open(report, 'w', encoding='utf-8') as handle:
        handle.write('\n'.join(triage_report(path=path, file=file, session=session,
                                             playbooks=playbooks,
                                             changelog=changelog)) + '\n')
    written.append(report)

    slim = os.path.join(target, 'session-%d-slim.log' % (session or 1))
    kept = write_slim(chosen, slim, levels=set(ESCALATION_LEVELS))
    written.append(slim)

    for source in profiler_logs_for_session(chosen, directory):
        copy = os.path.join(target, escalation_profiler_name(source))
        with open(source, 'r', encoding='utf-8', errors='replace') as reader, \
                open(copy, 'w', encoding='utf-8') as writer:
            for raw in reader:
                writer.write(redact(raw))
        written.append(copy)

    environment = os.path.join(target, 'environment.txt')
    with open(environment, 'w', encoding='utf-8') as handle:
        handle.write('agent version: %s\n' % (chosen.version or 'not stated'))
        handle.write('runtime: %s\n'
                     % (chosen.runtime or 'not stated (the agent logs it at DEBUG)'))
        handle.write('os: not recorded in an agent log line; it reaches the collector '
                     'in the connect payload\n')
        handle.write('pid: %d\n' % chosen.pid)
        handle.write('app domain(s): %s\n' % chosen.appdomain_label())
        handle.write('session: %s .. %s\n' % (fmt_ts(chosen.start), fmt_ts(chosen.end)))
        handle.write('lines: %d\n' % chosen.lines)
        handle.write('stated level: %s\n' % (chosen.log_level or 'not stated'))
        handle.write('observed levels: %s\n' % chosen.level_label())
        handle.write('flags: %s\n' % (','.join(chosen.flags()) or 'none'))
        handle.write('slim entries: %d (levels %s; DEBUG and FINEST are dropped)\n'
                     % (kept, ','.join(ESCALATION_LEVELS)))
    written.append(environment)

    if make_zip:
        archive = target + '.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as bundle:
            for item in written:
                bundle.write(item, os.path.join(os.path.basename(target),
                                                os.path.basename(item)))
        written.append(archive)

    return written, [ESCALATION_WARNING]


def cmd_summary(args):
    if args.escalation:
        written, warnings = build_escalation(
            path=args.path, file=args.file, session=args.session, ticket=args.ticket,
            out=args.out, make_zip=args.zip, playbooks=args.playbooks,
            changelog=args.changelog)
        for item in written:
            print('wrote %s' % item)
        print()
        for warning in warnings:
            print(warning)
        return
    if args.playbook is None:
        sys.exit('pass --playbook N, or --escalation')
    books = {b.id: b for b in load_playbooks(args.playbooks)}
    if args.playbook not in books:
        sys.exit('no playbook with id %d; run triage to see the matched ids'
                 % args.playbook)
    for line in customer_block(books[args.playbook]):
        print(line)


SHAPE_SUBS = [
    (re.compile(r'"[^"]*"'), '"..."'),
    (re.compile(r"'[^']*'"), "'...'"),
    (re.compile(r'\b0x[0-9a-fA-F]+\b'), '0xN'),
    (re.compile(r'\b\d+\b'), 'N'),
    (re.compile(r'\s+'), ' '),
]
SHAPE_MAX = 160
SHAPE_MIN = 20


def normalize_shape(message):
    text = message
    for pattern, replacement in SHAPE_SUBS:
        text = pattern.sub(replacement, text)
    return text.strip()[:SHAPE_MAX]


def candidate_signatures(session, playbooks, limit=5):
    """Frequent shapes at INFO or worse that no playbook already claims."""
    known = [s for book in playbooks for s in book.signatures]
    counts = {}
    for _path, _lineno, ts, level, pid, _tid, message, _raw in iter_entries(session.files):
        if ts is None or pid != session.pid:
            continue
        if not (session.start <= ts <= session.end):
            continue
        if LEVEL_ORDER[normalize_level(level)] > LEVEL_ORDER['INFO']:
            continue
        if any(signature in message for signature in known):
            continue
        shape = normalize_shape(message)
        if len(shape) < SHAPE_MIN:
            continue
        counts[shape] = counts.get(shape, 0) + 1
    ranked = sorted(counts.items(), key=lambda pair: (-pair[1], pair[0]))
    return ranked[:limit]


def draft_playbook_text(session, candidates, next_id):
    signatures = [shape for shape, _count in candidates] or ['REPLACE ME']
    body = []
    body.append('---')
    body.append('id: %d' % next_id)
    body.append('title: REPLACE ME with a one-line symptom name')
    body.append('tier: field')
    body.append('scope: managed')
    body.append('min_level: %s' % session_observed_level(session).lower())
    body.append('precedence: 80')
    body.append('signatures:')
    for signature in signatures:
        body.append('  - "%s"' % signature.replace('"', "'"))
    body.append('keywords: [REPLACE, ME]')
    body.append('observed_in: "%s"' % (session.version or 'unknown'))
    body.append('---')
    body.append('')
    body.append('**Symptom.** REPLACE ME with what the customer reported.')
    body.append('')
    body.append('**Verdict.** REPLACE ME with what the log proves.')
    body.append('')
    body.append('**Limit.** REPLACE ME with what the log does not prove.')
    body.append('')
    body.append('## Customer fix')
    body.append('')
    body.append('REPLACE ME with the instruction that resolved the ticket.')
    body.append('')
    body.append('## Next ask')
    body.append('')
    body.append('REPLACE ME, or "None." when the log settles it.')
    body.append('')
    body.append('<!-- candidate signature counts from the source session:')
    for shape, count in candidates:
        body.append('     x%-6d %s' % (count, shape))
    body.append('     Delete any signature that is not specific to this symptom. -->')
    body.append('')
    return '\n'.join(body)


def cmd_draft_playbook(args):
    managed = collect_managed(args.path, args.file)
    session = pick_session(managed, args.session)
    books = load_playbooks(args.playbooks)
    candidates = candidate_signatures(session, books)
    next_id = args.id or (max([b.id for b in books] + [SHIPPED_ID_MAX]) + 1)
    target_dir = out_dir_for(managed, args.out)
    target = os.path.join(target_dir, '%d-draft-playbook.md' % next_id)
    with open(target, 'w', encoding='utf-8') as handle:
        handle.write(draft_playbook_text(session, candidates, next_id))
    print('wrote %s' % target)
    print('%d candidate signature(s) from pid %d' % (len(candidates), session.pid))
    print('Replace every REPLACE ME, delete signatures that are not specific to the')
    print('symptom, then open a pull request against the field playbook directory.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest='command', required=True)

    sessions = subparsers.add_parser('sessions', help='list agent runs in a file or directory')
    sessions.add_argument('path')
    sessions.add_argument('--file', help='only files whose name contains this text')
    sessions.add_argument('--all', action='store_true',
                          help='list every session instead of the per-file summary')
    sessions.add_argument('--limit', type=int, default=40,
                          help='summarize per file above this many sessions (default 40)')
    sessions.set_defaults(func=cmd_sessions)

    triage = subparsers.add_parser('triage',
                                   help='one-call diagnosis: session, level, profiler, '
                                        'playbook match, version')
    triage.add_argument('path')
    triage.add_argument('--file', help='only files whose name contains this text')
    triage.add_argument('--session', type=int, help='session number, default newest')
    triage.add_argument('--playbooks', help='playbook directory, default the shipped set')
    triage.add_argument('--max-matched', type=int, default=5,
                        help='matched playbooks to print (default 5)')
    triage.add_argument('--evidence', type=int, default=2,
                        help='evidence lines per playbook (default 2)')
    triage.add_argument('--width', type=int, default=EVIDENCE_WIDTH,
                        help='evidence line width (default %d)' % EVIDENCE_WIDTH)
    triage.add_argument('--no-version', action='store_true',
                        help='skip the version block')
    triage.add_argument('--changelog',
                        help='path to a CHANGELOG.md to use instead of the resolved one')
    triage.add_argument('--worked-on', metavar='VERSION',
                        help='the agent version the customer says this worked on; '
                             'lists every change since it')
    triage.set_defaults(func=cmd_triage)

    extract = subparsers.add_parser('slim', aliases=['extract'],
                                    help='write a slim redacted file for one session')
    extract.add_argument('path')
    extract.add_argument('--file', help='only files whose name contains this text')
    extract.add_argument('--session', type=int, help='session number from "sessions"')
    extract.add_argument('--out', help='output directory (default: nrlog-work beside the log)')
    extract.add_argument('--level', help='comma-separated levels to keep, e.g. WARN,ERROR')
    extract.add_argument('--since', help='keep entries at or after this UTC time')
    extract.add_argument('--until', help='keep entries at or before this UTC time')
    extract.add_argument('--grep', help='keep entries whose message matches this regex')
    extract.add_argument('--max-width', type=int, default=400,
                         help='elide messages longer than this (default 400)')
    extract.set_defaults(func=cmd_extract)

    summary = subparsers.add_parser('summary',
                                    help='customer reply block, or an escalation packet')
    summary.add_argument('path')
    summary.add_argument('--file', help='only files whose name contains this text')
    summary.add_argument('--session', type=int)
    summary.add_argument('--playbook', type=int, help='playbook id for the customer block')
    summary.add_argument('--format', choices=('customer',), default='customer')
    summary.add_argument('--escalation', action='store_true',
                         help='build the escalation packet instead')
    summary.add_argument('--ticket', help='names the escalation folder')
    summary.add_argument('--zip', action='store_true', help='also write a zip')
    summary.add_argument('--playbooks', help='playbook directory')
    summary.add_argument('--out', help='output directory, default nrlog-work beside the log')
    summary.add_argument('--changelog',
                         help='path to a CHANGELOG.md to use instead of the resolved one')
    summary.set_defaults(func=cmd_summary)

    draft = subparsers.add_parser('draft-playbook',
                                  help='start a field playbook from an unmatched session')
    draft.add_argument('path')
    draft.add_argument('--file', help='only files whose name contains this text')
    draft.add_argument('--session', type=int)
    draft.add_argument('--playbooks', help='playbook directory')
    draft.add_argument('--out', help='output directory')
    draft.add_argument('--id', type=int, help='playbook id, default one above the highest')
    draft.set_defaults(func=cmd_draft_playbook)

    payloads = subparsers.add_parser('payloads', help='index collector calls')
    payloads.add_argument('path')
    payloads.add_argument('--file', help='only files whose name contains this text')
    payloads.add_argument('--session', type=int)
    payloads.add_argument('--limit', type=int, default=200)
    payloads.add_argument('--all', action='store_true',
                          help='include FINEST "Invoking" lines')
    payloads.set_defaults(func=cmd_payloads)

    body = subparsers.add_parser('body', help='print one request or response body')
    body.add_argument('path')
    body.add_argument('--file', help='only files whose name contains this text')
    body.add_argument('--request', required=True, help='request guid from "payloads"')
    body.add_argument('--direction', default='request',
                      choices=['request', 'response', 'headers'])
    body.add_argument('--max-bytes', type=int, default=20000)
    body.add_argument('--raw', action='store_true', help='skip JSON pretty printing')
    body.set_defaults(func=cmd_body)

    decode = subparsers.add_parser('decode', help='decode a base64 distributed-trace payload')
    decode.add_argument('--value', help='payload text (default: read stdin)')
    decode.add_argument('--key', help='encoding_key, for XOR-obfuscated CAT headers')
    decode.set_defaults(func=cmd_decode)

    profiler = subparsers.add_parser('profiler', help='summarize a profiler log')
    profiler.add_argument('path')
    profiler.add_argument('--limit', type=int, default=10)
    profiler.add_argument('--roster-above', type=int, default=8,
                          help='summarize as a roster above this many files (default 8)')
    profiler.set_defaults(func=cmd_profiler)

    instrumented = subparsers.add_parser('instrumented',
                                         help='list methods the profiler rewrote')
    instrumented.add_argument('path')
    instrumented.add_argument('--filter', help='regex over the method signature')
    instrumented.add_argument('--limit', type=int, default=200)
    instrumented.set_defaults(func=cmd_instrumented)

    args = parser.parse_args()
    args.func(args)


if __name__ == '__main__':
    main()
