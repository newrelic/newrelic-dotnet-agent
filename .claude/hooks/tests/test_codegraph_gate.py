#!/usr/bin/env python3
"""Unit tests for codegraph-gate.py. Run: python3 -m unittest discover -s .claude/hooks/tests"""

import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'codegraph-gate.py')
spec = importlib.util.spec_from_file_location('codegraph_gate', SCRIPT)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class CommandParsingTests(unittest.TestCase):

    def test_search_heads_are_detected(self):
        for command in ('grep -rn foo src', 'rg foo', 'find . -name "*.cs"', '/usr/bin/grep x f',
                        'cd src && grep foo', 'echo a; rg b', 'LC_ALL=C grep foo', 'grep.exe foo'):
            self.assertTrue(gate.is_search_command(command), command)

    def test_pipe_targets_and_other_commands_are_not_searches(self):
        for command in ('git log | grep fix', 'dotnet test | grep FAIL', 'git status', 'git grep foo',
                        'cat file', ''):
            self.assertFalse(gate.is_search_command(command), command)

    def test_codegraph_commands_are_detected(self):
        for command in ('codegraph explore "how does X work"', 'codegraph.cmd query Foo',
                        'cd repo && codegraph node Bar'):
            self.assertTrue(gate.is_codegraph_command(command), command)

    def test_other_codegraph_subcommands_do_not_count(self):
        for command in ('codegraph index', 'codegraph sync', 'codegraph', 'echo codegraph explore'):
            self.assertFalse(gate.is_codegraph_command(command), command)

    def test_unbalanced_quotes_fall_back_to_whitespace_split(self):
        self.assertTrue(gate.is_search_command('grep "unterminated'))


class GateTests(unittest.TestCase):

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.repo = os.path.join(self.tmp.name, 'repo')
        self.plain = os.path.join(self.tmp.name, 'plain')
        os.makedirs(os.path.join(self.repo, '.codegraph'))
        os.makedirs(os.path.join(self.repo, 'src'))
        os.makedirs(self.plain)
        open(os.path.join(self.repo, '.codegraph', 'codegraph.db'), 'w').close()
        gate.MARKER_DIR = os.path.join(self.tmp.name, 'markers')
        os.environ.pop('CODEGRAPH_NO_GATE_HOOK', None)

    def tearDown(self):
        os.environ.pop('CODEGRAPH_NO_GATE_HOOK', None)
        self.tmp.cleanup()

    def event(self, tool, tool_input, cwd=None, agent_id=None):
        data = {'session_id': 's1', 'tool_name': tool, 'tool_input': tool_input, 'cwd': cwd or self.repo}
        if agent_id:
            data['agent_id'] = agent_id
        return data

    def assertDenied(self, decision):
        self.assertIsNotNone(decision)
        output = decision['hookSpecificOutput']
        self.assertEqual(output['hookEventName'], 'PreToolUse')
        self.assertEqual(output['permissionDecision'], 'deny')
        self.assertIn('codegraph_explore', output['permissionDecisionReason'])

    def test_grep_glob_and_bash_search_are_denied_before_codegraph(self):
        self.assertDenied(gate.pre(self.event('Grep', {'pattern': 'Foo'})))
        self.assertDenied(gate.pre(self.event('Glob', {'pattern': '**/*.cs'})))
        self.assertDenied(gate.pre(self.event('Bash', {'command': 'grep -rn Foo src'})))

    def test_grep_path_inside_index_is_denied_from_unindexed_cwd(self):
        self.assertDenied(gate.pre(self.event('Grep', {'pattern': 'Foo', 'path': os.path.join(self.repo, 'src')},
                                              cwd=self.plain)))

    def test_relative_grep_path_resolves_against_cwd(self):
        self.assertDenied(gate.pre(self.event('Grep', {'pattern': 'Foo', 'path': 'src'})))

    # Bash strips unquoted backslashes, so command strings carry forward-slash paths.
    def bash_path(self, *parts):
        return os.path.join(*parts).replace('\\', '/')

    def test_bash_search_after_cd_out_of_index_is_allowed(self):
        command = 'cd %s && grep -n Foo notes.md' % self.bash_path(self.plain)
        self.assertIsNone(gate.pre(self.event('Bash', {'command': command})))

    def test_bash_absolute_path_outside_index_is_allowed(self):
        target = self.bash_path(self.plain, 'not-yet-written.md')
        self.assertIsNone(gate.pre(self.event('Bash', {'command': 'echo a > x && grep -n Foo %s | cut -c1-9' % target})))

    def test_bash_path_inside_index_is_denied(self):
        plain, src = self.bash_path(self.plain), self.bash_path(self.repo, 'src')
        self.assertDenied(gate.pre(self.event('Bash', {'command': 'grep Foo %s %s' % (plain, src)})))
        self.assertDenied(gate.pre(self.event('Bash', {'command': 'grep Foo %s' % src}, cwd=self.plain)))

    def test_bash_redirect_target_is_not_a_search_path(self):
        out = self.bash_path(self.plain, 'out.txt')
        self.assertDenied(gate.pre(self.event('Bash', {'command': 'grep -rn Foo > %s 2>&1' % out})))

    @unittest.skipUnless(os.name == 'nt', 'Git Bash drive paths exist only on Windows')
    def test_git_bash_drive_path_is_converted(self):
        self.assertEqual(gate._native_path('/c/tmp/x.md'), 'C:/tmp/x.md')
        self.assertEqual(gate._native_path('/c'), 'C:/')

    def test_unindexed_directory_is_allowed(self):
        self.assertIsNone(gate.pre(self.event('Grep', {'pattern': 'Foo'}, cwd=self.plain)))

    def test_non_search_tools_and_commands_are_allowed(self):
        self.assertIsNone(gate.pre(self.event('Read', {'file_path': 'x'})))
        self.assertIsNone(gate.pre(self.event('Bash', {'command': 'git status'})))

    def test_mcp_codegraph_call_unlocks_the_session(self):
        gate.post(self.event('mcp__codegraph__codegraph_explore', {'query': 'Foo'}))
        self.assertIsNone(gate.pre(self.event('Grep', {'pattern': 'Foo'})))

    def test_bash_codegraph_call_unlocks_the_session(self):
        gate.post(self.event('Bash', {'command': 'codegraph explore Foo'}))
        self.assertIsNone(gate.pre(self.event('Bash', {'command': 'rg Foo'})))

    def test_other_tools_do_not_unlock(self):
        gate.post(self.event('Bash', {'command': 'git status'}))
        gate.post(self.event('Read', {'file_path': 'x'}))
        self.assertDenied(gate.pre(self.event('Grep', {'pattern': 'Foo'})))

    def test_marker_is_per_agent(self):
        gate.post(self.event('mcp__codegraph__codegraph_explore', {}))
        self.assertDenied(gate.pre(self.event('Grep', {'pattern': 'Foo'}, agent_id='agent-1')))
        gate.post(self.event('mcp__codegraph__codegraph_explore', {}, agent_id='agent-1'))
        self.assertIsNone(gate.pre(self.event('Grep', {'pattern': 'Foo'}, agent_id='agent-1')))

    def test_kill_switch_allows_everything(self):
        os.environ['CODEGRAPH_NO_GATE_HOOK'] = '1'
        self.assertIsNone(gate.pre(self.event('Grep', {'pattern': 'Foo'})))


class EntryPointTests(unittest.TestCase):

    def run_script(self, mode, stdin_bytes):
        return subprocess.run([sys.executable, SCRIPT, mode], input=stdin_bytes, capture_output=True)

    def test_garbage_stdin_exits_zero_silently(self):
        for mode in ('pre', 'post', 'unknown'):
            result = self.run_script(mode, b'not json')
            self.assertEqual(result.returncode, 0)
            self.assertEqual(result.stdout, b'')

    def test_pre_prints_deny_json_in_indexed_repo(self):
        with tempfile.TemporaryDirectory() as repo:
            os.makedirs(os.path.join(repo, '.codegraph'))
            open(os.path.join(repo, '.codegraph', 'codegraph.db'), 'w').close()
            payload = {'session_id': 'entry-%d' % os.getpid(), 'tool_name': 'Grep',
                       'tool_input': {'pattern': 'Foo'}, 'cwd': repo}
            result = self.run_script('pre', json.dumps(payload).encode('utf-8'))
            self.assertEqual(result.returncode, 0)
            decision = json.loads(result.stdout.decode('utf-8'))
            self.assertEqual(decision['hookSpecificOutput']['permissionDecision'], 'deny')


if __name__ == '__main__':
    unittest.main()
