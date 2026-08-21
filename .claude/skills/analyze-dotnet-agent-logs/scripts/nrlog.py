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
from datetime import datetime, timedelta

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

INSTRUMENTING_RE = re.compile(r'^Instrumenting (?:API |helper )?method: (.*)$')

MERGE_GAP = timedelta(minutes=5)


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

    kept = 0
    with open(target, 'w', encoding='utf-8') as sink:
        sink.write('# nrlog.py slim extract: pid %d, %s .. %s, flags: %s\n'
                   % (session.pid, fmt_ts(session.start), fmt_ts(session.end),
                      ','.join(session.flags()) or 'none'))
        sink.write('# source: %s\n' % ', '.join(os.path.basename(f) for f in session.files))
        sink.write('# payload bodies stripped; secrets redacted\n')
        ours = False
        for path, _lineno, ts, level, pid, tid, message, raw in iter_entries(session.files):
            if ts is None:
                if ours:
                    sink.write(redact(raw[:args.max_width]) + '\n')
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
                level, pid, tid, redact(slim_message(message, args.max_width))))
            kept += 1
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


def profiler_roster(paths, args):
    """Aggregate view for a directory of profiler logs, of which most are noise."""
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

    extract = subparsers.add_parser('extract', help='write a slim redacted file for one session')
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
