"""Tests for the support-log-plugin export script.

Run from the repository root:
    python -m unittest discover -s tools/support-log-plugin/tests -v
"""

import contextlib
import io
import json
import os
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL_DIR = os.path.dirname(HERE)
sys.path.insert(0, TOOL_DIR)

import export  # noqa: E402


FIELD_STUB = '''---
id: %d
title: %s
tier: field
scope: managed
min_level: info
precedence: 50
signatures:
  - "%s"
keywords: [stub]
observed_in: "10.40.1"
---

**Symptom.** Stub.

## Customer fix

Stub.

## Next ask

Stub.
'''


def write_playbook(directory, name, text):
    os.makedirs(directory, exist_ok=True)
    path = os.path.join(directory, name)
    with open(path, 'w', encoding='ascii', newline='\n') as handle:
        handle.write(text)
    return path


class RepoDiscoveryTests(unittest.TestCase):

    def test_repo_root_holds_the_skill_and_the_changelog(self):
        root = export.repo_root()
        self.assertTrue(os.path.isdir(os.path.join(
            root, '.claude', 'skills', 'analyze-dotnet-agent-logs')))
        self.assertTrue(os.path.isfile(os.path.join(
            root, 'src', 'Agent', 'CHANGELOG.md')))

    def test_import_nrlog_exposes_the_part_one_loader(self):
        nrlog = export.import_nrlog()
        for name in ('load_playbooks', 'default_playbook_dir'):
            self.assertTrue(hasattr(nrlog, name), name)


class VerifiedLoadTests(unittest.TestCase):

    def test_the_real_verified_set_loads_and_is_all_verified(self):
        nrlog = export.import_nrlog()
        books = export.load_verified(nrlog)
        self.assertGreaterEqual(len(books), 9)
        self.assertEqual(sorted({b.tier for b in books}), ['verified'])

    def test_every_verified_id_is_below_one_hundred(self):
        nrlog = export.import_nrlog()
        for book in export.load_verified(nrlog):
            self.assertLess(book.id, 100, book.path)


class FieldLoadTests(unittest.TestCase):

    def setUp(self):
        self.target = tempfile.mkdtemp(prefix='nrlog-export-')
        self.field = os.path.join(self.target, 'teams', 'dotnet-agent',
                                  'playbooks', 'field')
        os.makedirs(self.field)
        self.nrlog = export.import_nrlog()

    def tearDown(self):
        shutil.rmtree(self.target, ignore_errors=True)

    def test_no_field_directory_is_not_an_error(self):
        empty = tempfile.mkdtemp(prefix='nrlog-export-bare-')
        try:
            books, warnings = export.load_field(self.nrlog, empty)
            self.assertEqual(books, [])
            self.assertEqual(warnings, [])
        finally:
            shutil.rmtree(empty, ignore_errors=True)

    def test_a_good_field_playbook_loads(self):
        write_playbook(self.field, '100-stub.md',
                       FIELD_STUB % (100, 'Stub one', 'stub signature one'))
        books, warnings = export.load_field(self.nrlog, self.target)
        self.assertEqual([b.id for b in books], [100])
        self.assertEqual(books[0].tier, 'field')
        self.assertEqual(warnings, [])

    def test_one_broken_field_playbook_is_skipped_and_warned_about(self):
        write_playbook(self.field, '100-stub.md',
                       FIELD_STUB % (100, 'Stub one', 'stub signature one'))
        write_playbook(self.field, '101-broken.md',
                       'no frontmatter here at all\n')
        books, warnings = export.load_field(self.nrlog, self.target)
        self.assertEqual([b.id for b in books], [100])
        self.assertEqual(len(warnings), 1)
        self.assertIn('101-broken.md', warnings[0])


class IdRuleTests(unittest.TestCase):

    class Fake(object):

        def __init__(self, ident, tier, path):
            self.id = ident
            self.tier = tier
            self.path = path
            self.title = 'Fake %d' % ident

    def test_a_legal_split_set_has_no_errors(self):
        verified = [self.Fake(1, 'verified', 'a.md'),
                    self.Fake(9, 'verified', 'b.md')]
        field = [self.Fake(100, 'field', 'c.md')]
        self.assertEqual(export.check_ids(verified, field), [])

    def test_a_field_id_below_one_hundred_is_an_error(self):
        errors = export.check_ids([self.Fake(1, 'verified', 'a.md')],
                                  [self.Fake(4, 'field', 'c.md')])
        self.assertEqual(len(errors), 1)
        self.assertIn('c.md', errors[0])
        self.assertIn('100', errors[0])

    def test_a_verified_id_at_or_above_one_hundred_is_an_error(self):
        errors = export.check_ids([self.Fake(100, 'verified', 'a.md')], [])
        self.assertEqual(len(errors), 1)
        self.assertIn('a.md', errors[0])

    def test_a_duplicate_id_within_a_tier_is_an_error(self):
        errors = export.check_ids([self.Fake(1, 'verified', 'a.md'),
                                   self.Fake(1, 'verified', 'b.md')], [])
        self.assertEqual(len(errors), 1)
        self.assertIn('duplicate', errors[0].lower())

    def test_a_duplicate_id_across_tiers_is_an_error(self):
        errors = export.check_ids([self.Fake(100, 'verified', 'a.md')],
                                  [self.Fake(100, 'field', 'c.md')])
        self.assertTrue(errors)


class LintTests(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        scaffold = os.path.join(TOOL_DIR, 'scaffold')
        if scaffold not in sys.path:
            sys.path.insert(0, scaffold)
        import lint_playbooks
        cls.lint = lint_playbooks

    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='nrlog-lint-')

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def lint_text(self, name, text):
        path = write_playbook(self.dir, name, text)
        return self.lint.lint_file(path)

    def test_a_good_field_playbook_passes(self):
        errors = self.lint_text(
            '100-stub.md', FIELD_STUB % (100, 'Stub', 'a literal log fragment'))
        self.assertEqual(errors, [])

    def test_missing_frontmatter_fails(self):
        errors = self.lint_text('100-stub.md', 'nothing here\n')
        self.assertTrue(any('frontmatter' in e for e in errors), errors)

    def test_a_field_id_below_one_hundred_fails(self):
        errors = self.lint_text(
            '004-stub.md', FIELD_STUB % (4, 'Stub', 'a literal log fragment'))
        self.assertTrue(any('100' in e for e in errors), errors)

    def test_an_id_that_disagrees_with_the_filename_fails(self):
        errors = self.lint_text(
            '101-stub.md', FIELD_STUB % (100, 'Stub', 'a literal log fragment'))
        self.assertTrue(any('filename' in e for e in errors), errors)

    def test_a_missing_observed_in_fails_for_a_field_playbook(self):
        text = FIELD_STUB % (100, 'Stub', 'a literal log fragment')
        text = text.replace('observed_in: "10.40.1"\n', '')
        errors = self.lint_text('100-stub.md', text)
        self.assertTrue(any('observed_in' in e for e in errors), errors)

    def test_no_signature_and_no_level_max_fails(self):
        text = FIELD_STUB % (100, 'Stub', 'a literal log fragment')
        text = text.replace('signatures:\n  - "a literal log fragment"\n', '')
        errors = self.lint_text('100-stub.md', text)
        self.assertTrue(any('level_max' in e for e in errors), errors)

    def test_a_level_max_without_signatures_passes(self):
        text = FIELD_STUB % (100, 'Stub', 'a literal log fragment')
        text = text.replace('signatures:\n  - "a literal log fragment"\n',
                            'level_max: info\n')
        errors = self.lint_text('100-stub.md', text)
        self.assertEqual(errors, [])

    def test_a_regex_shaped_signature_fails(self):
        for shape in ('^started', 'agent v\\d+', 'connect.*failed',
                      'level (?:INFO|WARN)', 'pid [0-9]+'):
            errors = self.lint_text(
                '100-stub.md', FIELD_STUB % (100, 'Stub', shape))
            self.assertTrue(any('literal' in e for e in errors),
                            '%s: %s' % (shape, errors))

    def test_a_signature_containing_an_alternation_fails(self):
        errors = self.lint_text(
            '100-stub.md',
            FIELD_STUB % (100, 'Stub', 'started | failed pattern'))
        self.assertTrue(any('alternation' in e for e in errors), errors)

    def test_a_literal_signature_with_parentheses_passes(self):
        errors = self.lint_text(
            '100-stub.md',
            FIELD_STUB % (100, 'Stub', 'started (pid 8124) on app domain'))
        self.assertEqual(errors, [])

    def test_a_short_signature_fails(self):
        errors = self.lint_text('100-stub.md',
                                FIELD_STUB % (100, 'Stub', 'error'))
        self.assertTrue(any('too short' in e for e in errors), errors)

    def test_a_missing_heading_fails(self):
        text = FIELD_STUB % (100, 'Stub', 'a literal log fragment')
        text = text.replace('## Next ask', '## Followup')
        errors = self.lint_text('100-stub.md', text)
        self.assertTrue(any('Next ask' in e for e in errors), errors)

    def test_an_empty_heading_fails(self):
        text = FIELD_STUB % (100, 'Stub', 'a literal log fragment')
        text = text.replace('## Next ask\n\nStub.\n', '## Next ask\n')
        errors = self.lint_text('100-stub.md', text)
        self.assertTrue(any('empty' in e for e in errors), errors)

    def test_a_non_ascii_byte_fails(self):
        text = FIELD_STUB % (100, 'Stub', 'a literal log fragment')
        path = os.path.join(self.dir, '100-stub.md')
        with open(path, 'wb') as handle:
            handle.write(text.replace('Stub.', 'Stub \xe2\x80\x94 dash.')
                         .encode('latin-1'))
        errors = self.lint.lint_file(path)
        self.assertTrue(any('ASCII' in e for e in errors), errors)

    def test_main_returns_one_on_a_duplicate_id(self):
        write_playbook(self.dir, '100-a.md',
                       FIELD_STUB % (100, 'A', 'a literal log fragment'))
        write_playbook(self.dir, '100-b.md',
                       FIELD_STUB % (100, 'B', 'another literal fragment'))
        self.assertEqual(self.lint.main([self.dir]), 1)

    def test_main_returns_zero_on_a_clean_directory(self):
        write_playbook(self.dir, '100-a.md',
                       FIELD_STUB % (100, 'A', 'a literal log fragment'))
        self.assertEqual(self.lint.main([self.dir]), 0)

    def test_main_returns_zero_on_a_missing_directory(self):
        self.assertEqual(
            self.lint.main([os.path.join(self.dir, 'absent')]), 0)

    def test_the_real_verified_set_passes_the_lint(self):
        directory = os.path.join(export.skill_dir(), 'references', 'playbooks')
        self.assertEqual(self.lint.main([directory]), 0)


class TreeTests(unittest.TestCase):

    FOREIGN = {
        'README.md': '# nr-agents\n\nHand-authored, org-wide.\n',
        'plugins/browser-log-triage/.claude-plugin/plugin.json':
            '{"name": "browser-log-triage"}\n',
        '.github/workflows/lint-browser-playbooks.yml': 'name: Browser\n',
        'teams/browser-agent/playbooks/field/100-theirs.md': 'theirs\n',
    }

    def setUp(self):
        self.target = tempfile.mkdtemp(prefix='nrlog-target-')
        os.makedirs(self.field_dir())
        for relative, text in self.FOREIGN.items():
            path = os.path.join(self.target, *relative.split('/'))
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, 'w', encoding='ascii', newline='\n') as handle:
                handle.write(text)
        self.foreign_before = self.fingerprint()

    def tearDown(self):
        shutil.rmtree(self.target, ignore_errors=True)

    def field_dir(self):
        return os.path.join(self.target, *export.FIELD_REL)

    def fingerprint(self):
        found = {}
        for relative in self.FOREIGN:
            path = os.path.join(self.target, *relative.split('/'))
            with open(path, 'rb') as handle:
                found[relative] = handle.read()
        return found

    def actual_tree(self):
        found = []
        for base, dirs, names in os.walk(self.target):
            dirs[:] = [d for d in dirs if d != '.git']
            for name in names:
                full = os.path.join(base, name)
                found.append(os.path.relpath(full, self.target)
                             .replace(os.sep, '/'))
        return sorted(found)

    def owned_paths(self):
        with open(os.path.join(HERE, 'expected-tree.txt'), 'r',
                  encoding='ascii') as handle:
            return sorted(l.strip() for l in handle if l.strip())

    def marketplace(self):
        path = os.path.join(self.target, '.claude-plugin',
                            'marketplace.json')
        with open(path, 'r', encoding='ascii') as handle:
            return json.load(handle)

    def write_marketplace(self, payload):
        directory = os.path.join(self.target, '.claude-plugin')
        os.makedirs(directory, exist_ok=True)
        with open(os.path.join(directory, 'marketplace.json'), 'w',
                  encoding='ascii', newline='\n') as handle:
            json.dump(payload, handle, indent=2)
            handle.write('\n')

    def test_every_owned_path_is_present_after_an_export(self):
        self.assertEqual(export.run_export(self.target, False, 90), 0)
        actual = self.actual_tree()
        for relative in self.owned_paths():
            self.assertIn(relative, actual)

    def test_no_unexpected_path_appears_under_the_plugin_directory(self):
        export.run_export(self.target, False, 90)
        owned = set(self.owned_paths())
        under = set(p for p in self.actual_tree()
                    if p.startswith('plugins/dotnet-log-triage/'))
        self.assertEqual(sorted(under - owned), [])

    def test_the_export_changes_no_file_it_does_not_own(self):
        export.run_export(self.target, False, 90)
        self.assertEqual(self.fingerprint(), self.foreign_before)

    def test_export_is_idempotent(self):
        export.run_export(self.target, False, 90)
        first = self.actual_tree()
        self.assertEqual(export.run_export(self.target, False, 90), 0)
        self.assertEqual(self.actual_tree(), first)

    def test_a_dry_run_writes_nothing(self):
        before = self.actual_tree()
        self.assertEqual(export.run_export(self.target, True, 90), 0)
        self.assertEqual(self.actual_tree(), before)

    def test_the_marketplace_is_written_whole_when_it_is_absent(self):
        export.run_export(self.target, False, 90)
        data = self.marketplace()
        self.assertEqual(data['name'], 'nr-agents')
        self.assertEqual([p['name'] for p in data['plugins']],
                         ['dotnet-log-triage'])
        self.assertNotIn('pluginRoot', json.dumps(data))

    def test_an_unrelated_marketplace_entry_survives_a_round_trip(self):
        self.write_marketplace({
            'name': 'nr-agents',
            'owner': {'name': 'New Relic Agent Teams'},
            'metadata': {'theirKey': 'keep me'},
            'plugins': [
                {'name': 'browser-log-triage',
                 'source': './plugins/browser-log-triage'},
                {'name': 'dotnet-log-triage', 'source': './stale'},
            ],
        })
        self.assertEqual(export.run_export(self.target, False, 90), 0)
        data = self.marketplace()
        self.assertEqual([p['name'] for p in data['plugins']],
                         ['browser-log-triage', 'dotnet-log-triage'])
        self.assertEqual(data['metadata']['theirKey'], 'keep me')
        ours = [p for p in data['plugins']
                if p['name'] == 'dotnet-log-triage'][0]
        self.assertEqual(ours['source'], './plugins/dotnet-log-triage')

    def test_another_teams_broken_entry_does_not_fail_the_export(self):
        self.write_marketplace({
            'name': 'nr-agents',
            'owner': {'name': 'New Relic Agent Teams'},
            'plugins': [{'name': 'browser-log-triage'}],
        })
        self.assertEqual(export.run_export(self.target, False, 90), 0)
        self.assertEqual([p['name'] for p in self.marketplace()['plugins']],
                         ['browser-log-triage', 'dotnet-log-triage'])

    def test_a_field_playbook_reaches_the_exported_tree(self):
        write_playbook(self.field_dir(), '100-stub.md',
                       FIELD_STUB % (100, 'Stub', 'a literal log fragment'))
        export.run_export(self.target, False, 90)
        self.assertIn('plugins/dotnet-log-triage/skills/'
                      'triage-dotnet-agent-logs/references/playbooks/'
                      '100-stub.md', self.actual_tree())

    def test_the_field_source_directory_is_never_written_to(self):
        field = self.field_dir()
        write_playbook(field, '100-stub.md',
                       FIELD_STUB % (100, 'Stub', 'a literal log fragment'))
        before = sorted(os.listdir(field))
        export.run_export(self.target, False, 90)
        self.assertEqual(sorted(os.listdir(field)), before)

    def test_a_field_playbook_with_a_verified_id_is_skipped_with_a_warning(self):
        write_playbook(self.field_dir(), '004-stub.md',
                       FIELD_STUB % (4, 'Stub', 'a literal log fragment'))
        buffer = io.StringIO()
        with contextlib.redirect_stdout(buffer):
            self.assertEqual(export.run_export(self.target, False, 90), 0)
        output = buffer.getvalue()
        self.assertIn('004-stub.md', output)
        self.assertIn('warning', output)
        self.assertNotIn('playbooks/004-stub.md', '\n'.join(self.actual_tree()))

    def test_a_removed_playbook_disappears_on_the_next_export(self):
        field = self.field_dir()
        write_playbook(field, '100-stub.md',
                       FIELD_STUB % (100, 'Stub', 'a literal log fragment'))
        export.run_export(self.target, False, 90)
        os.remove(os.path.join(field, '100-stub.md'))
        export.run_export(self.target, False, 90)
        self.assertNotIn('plugins/dotnet-log-triage/skills/'
                         'triage-dotnet-agent-logs/references/playbooks/'
                         '100-stub.md', self.actual_tree())

    def test_no_host_path_reaches_the_exported_tree(self):
        export.run_export(self.target, False, 90)
        offenders = export.scan_for_host_paths(self.target,
                                               self.owned_paths())
        self.assertEqual(offenders, [])

    def test_the_exported_tree_is_ascii(self):
        export.run_export(self.target, False, 90)
        for relative in self.owned_paths():
            with open(os.path.join(self.target, relative), 'rb') as handle:
                raw = handle.read()
            try:
                raw.decode('ascii')
            except UnicodeDecodeError as error:
                self.fail('%s is not ASCII at byte %d'
                          % (relative, error.start))

    def test_the_exported_manifest_carries_a_bumped_version(self):
        export.run_export(self.target, False, 90)
        path = os.path.join(self.target, 'plugins', 'dotnet-log-triage',
                            '.claude-plugin', 'plugin.json')
        with open(path, 'r', encoding='ascii') as handle:
            data = json.load(handle)
        self.assertEqual(data['name'], 'dotnet-log-triage')
        self.assertRegex(data['version'], r'^\d+\.\d+\.\d+$')

    def test_the_exported_skill_declares_its_own_name(self):
        export.run_export(self.target, False, 90)
        path = os.path.join(self.target, 'plugins', 'dotnet-log-triage',
                            'skills', 'triage-dotnet-agent-logs', 'SKILL.md')
        with open(path, 'r', encoding='ascii') as handle:
            head = handle.read(400)
        self.assertIn('name: triage-dotnet-agent-logs', head)

    def test_the_exported_script_is_the_part_one_script(self):
        export.run_export(self.target, False, 90)
        source = os.path.join(export.skill_dir(), 'scripts', 'nrlog.py')
        copied = os.path.join(self.target, 'plugins', 'dotnet-log-triage',
                              'skills', 'triage-dotnet-agent-logs', 'scripts',
                              'nrlog.py')
        with open(source, 'rb') as handle:
            expected = handle.read()
        with open(copied, 'rb') as handle:
            self.assertEqual(handle.read(), expected)


if __name__ == '__main__':
    unittest.main()
