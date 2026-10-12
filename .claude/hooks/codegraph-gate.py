#!/usr/bin/env python3
"""Claude Code hook: require one CodeGraph query before text search in an indexed repo.

Usage (from .claude/settings.json): codegraph-gate.py pre | post

  pre   PreToolUse on Grep|Glob|Bash. Denies a search until this session (or
        subagent) has made a CodeGraph call.
  post  PostToolUse / PostToolUseFailure. Records a CodeGraph call.

Same behavior as the upstream proposal colbymchenry/codegraph#2313.
CODEGRAPH_NO_GATE_HOOK=1 turns it off. Any error exits 0 with no output.
"""

import json
import os
import re
import shlex
import sys
import tempfile

SEARCH_HEADS = {'grep', 'egrep', 'fgrep', 'rg', 'find', 'fd', 'ag', 'ack'}
CODEGRAPH_SUBCOMMANDS = {'explore', 'query', 'node', 'context', 'callers', 'callees', 'impact', 'files'}
MARKER_DIR = os.path.join(tempfile.gettempdir(), 'claude-codegraph-gate')

DENY_REASON = (
    'This repo has a CodeGraph index. Call codegraph_explore first (or `codegraph explore "<question>"` '
    'in Bash): it returns the verbatim source plus callers in one call. After one CodeGraph call, '
    'Grep/Glob/grep/find are allowed for the rest of the session, for literal text and for file types '
    'CodeGraph does not index (.md, .sh).'
)


def find_index_root(start):
    current = os.path.abspath(start)
    while True:
        if os.path.isfile(os.path.join(current, '.codegraph', 'codegraph.db')):
            return current
        parent = os.path.dirname(current)
        if parent == current:
            return None
        current = parent


def _tokens(text):
    try:
        return shlex.split(text, posix=True)
    except ValueError:
        return text.split()


def _program(token):
    name = re.split(r'[\\/]', token)[-1].lower()
    for suffix in ('.exe', '.cmd', '.bat'):
        if name.endswith(suffix):
            return name[:-len(suffix)]
    return name


def command_heads(command):
    """Yield (program, args) for the first command of each statement; pipe targets are skipped."""
    for statement in re.split(r'&&|\|\||;|\n', command):
        first = statement.split('|', 1)[0]
        tokens = [t for t in _tokens(first) if t not in ('(', '{')]
        while tokens and re.match(r'^[A-Za-z_][A-Za-z0-9_]*=', tokens[0]):
            tokens.pop(0)
        if tokens:
            yield _program(tokens[0]), tokens[1:]


def is_search_command(command):
    return any(program in SEARCH_HEADS for program, _ in command_heads(command))


def is_codegraph_command(command):
    return any(program == 'codegraph' and args and args[0] in CODEGRAPH_SUBCOMMANDS
               for program, args in command_heads(command))


def marker_path(data):
    key = '%s-%s' % (data.get('session_id') or 'nosession', data.get('agent_id') or 'main')
    return os.path.join(MARKER_DIR, re.sub(r'[^A-Za-z0-9_.-]', '_', key))


def pre(data):
    if os.environ.get('CODEGRAPH_NO_GATE_HOOK') == '1':
        return None
    tool = data.get('tool_name', '')
    tool_input = data.get('tool_input') or {}
    cwd = data.get('cwd') or os.getcwd()
    if tool == 'Bash':
        if not is_search_command(tool_input.get('command', '')):
            return None
        start = cwd
    elif tool in ('Grep', 'Glob'):
        start = os.path.join(cwd, tool_input['path']) if tool_input.get('path') else cwd
    else:
        return None
    if find_index_root(start) is None or os.path.exists(marker_path(data)):
        return None
    return {'hookSpecificOutput': {
        'hookEventName': 'PreToolUse',
        'permissionDecision': 'deny',
        'permissionDecisionReason': DENY_REASON,
    }}


def post(data):
    tool = data.get('tool_name', '')
    if tool.startswith('mcp__') and 'codegraph' in tool.lower():
        used = True
    elif tool == 'Bash':
        used = is_codegraph_command((data.get('tool_input') or {}).get('command', ''))
    else:
        used = False
    if used:
        os.makedirs(MARKER_DIR, exist_ok=True)
        with open(marker_path(data), 'w', encoding='utf-8') as marker:
            marker.write(tool)


def main(argv):
    try:
        data = json.loads(sys.stdin.buffer.read().decode('utf-8', errors='replace'))
        if argv[1:] == ['pre']:
            decision = pre(data)
            if decision:
                sys.stdout.write(json.dumps(decision))
        elif argv[1:] == ['post']:
            post(data)
    except Exception:
        pass
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
