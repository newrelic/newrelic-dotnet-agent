"""Tests for nrlog.py. Standard library only: python -m unittest discover tests"""

import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from unittest import mock

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', 'scripts'))

import nrlog


VALID = '''---
id: 2
title: .NET Framework allow-list rejection
tier: verified
scope: profiler
min_level: info
precedence: 20
signatures:
  - "is not configured to be instrumented"
  - "should not be instrumented, unloading profiler"
keywords: [allow-list, included application]
verified_versions: "10.54.0"
---

Body text.

## Customer fix

Set NEW_RELIC_INCLUDED_APPLICATION_NAMES=MyApp.exe.

## Next ask

None. The log settles it.
'''


class ParseFrontmatterTests(unittest.TestCase):

    def test_returns_fields_and_body(self):
        fields, body = nrlog.parse_frontmatter(VALID)
        self.assertEqual(fields['id'], 2)
        self.assertEqual(fields['tier'], 'verified')
        self.assertEqual(fields['precedence'], 20)
        self.assertEqual(fields['signatures'], [
            'is not configured to be instrumented',
            'should not be instrumented, unloading profiler'])
        self.assertEqual(fields['keywords'], ['allow-list', 'included application'])
        self.assertIn('## Customer fix', body)
        self.assertNotIn('---', body.splitlines()[0:1])

    def test_empty_list_field_is_a_list_not_a_string(self):
        fields, _body = nrlog.parse_frontmatter(
            VALID.replace('signatures:\n  - "is not configured to be instrumented"\n'
                          '  - "should not be instrumented, unloading profiler"\n',
                          'signatures:\nlevel_max: info\n'))
        self.assertEqual(fields['signatures'], [])
        self.assertEqual(fields['level_max'], 'info')

    def test_missing_frontmatter_raises(self):
        with self.assertRaises(ValueError):
            nrlog.parse_frontmatter('no frontmatter here\n')

    def test_unterminated_frontmatter_raises(self):
        with self.assertRaises(ValueError):
            nrlog.parse_frontmatter('---\nid: 1\n')

    def test_unparseable_line_raises(self):
        with self.assertRaises(ValueError):
            nrlog.parse_frontmatter('---\nid 1\n---\n\nbody\n')


class ValidatePlaybookTests(unittest.TestCase):

    def fields(self, **changes):
        fields, _body = nrlog.parse_frontmatter(VALID)
        for key, value in changes.items():
            if value is None:
                fields.pop(key, None)
            else:
                fields[key] = value
        return fields

    def test_valid_playbook_has_no_errors(self):
        self.assertEqual(nrlog.validate_playbook(self.fields(), 'x.md'), [])

    def test_missing_required_field_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(scope=None), 'x.md')
        self.assertTrue(any('scope' in e for e in errors))

    def test_bad_scope_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(scope='sideways'), 'x.md')
        self.assertTrue(any('scope' in e for e in errors))

    def test_bad_tier_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(tier='rumour'), 'x.md')
        self.assertTrue(any('tier' in e for e in errors))

    def test_regex_looking_signature_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(signatures=['pid: (\\d+)']), 'x.md')
        self.assertTrue(any('literal' in e for e in errors))

    def test_placeholder_brace_signature_is_reported(self):
        errors = nrlog.validate_playbook(
            self.fields(signatures=['Error initializing CLR profiler info: {hr}']), 'x.md')
        self.assertTrue(any('literal' in e for e in errors))

    def test_anchored_signature_is_reported(self):
        errors = nrlog.validate_playbook(
            self.fields(signatures=['^Agent fully connected']), 'x.md')
        self.assertTrue(any('literal' in e for e in errors))

    def test_parentheses_and_backslashes_are_allowed(self):
        good = ['Current TLS Configuration (System.Net.ServicePointManager.SecurityProtocol)',
                'This process (C:\\Apps\\MyApp.exe) is not configured to be instrumented']
        self.assertEqual(nrlog.validate_playbook(self.fields(signatures=good), 'x.md'), [])

    def test_field_tier_requires_observed_in(self):
        errors = nrlog.validate_playbook(
            self.fields(tier='field', verified_versions=None), 'x.md')
        self.assertTrue(any('observed_in' in e for e in errors))

    def test_verified_tier_requires_verified_versions(self):
        errors = nrlog.validate_playbook(self.fields(verified_versions=None), 'x.md')
        self.assertTrue(any('verified_versions' in e for e in errors))

    def test_min_level_alias_is_accepted(self):
        self.assertEqual(nrlog.validate_playbook(self.fields(min_level='all'), 'x.md'), [])

    def test_unknown_min_level_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(min_level='banana'), 'x.md')
        self.assertTrue(any('min_level' in e for e in errors))

    def test_level_max_alone_satisfies_the_signature_requirement(self):
        self.assertEqual(
            nrlog.validate_playbook(self.fields(signatures=[], level_max='info'), 'x.md'), [])

    def test_neither_signatures_nor_level_max_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(signatures=[]), 'x.md')
        self.assertTrue(any('level_max' in e for e in errors))

    def test_verified_tier_above_the_shipped_id_range_is_reported(self):
        errors = nrlog.validate_playbook(self.fields(id=100), 'x.md')
        self.assertTrue(any('id' in e for e in errors))

    def test_field_tier_inside_the_shipped_id_range_is_reported(self):
        errors = nrlog.validate_playbook(
            self.fields(id=42, tier='field', observed_in='NR-1', verified_versions=None),
            'x.md')
        self.assertTrue(any('id' in e for e in errors))

    def test_field_tier_above_the_shipped_id_range_is_valid(self):
        self.assertEqual(nrlog.validate_playbook(
            self.fields(id=100, tier='field', observed_in='NR-1',
                        verified_versions=None), 'x.md'), [])

    def test_condition_flag_alone_satisfies_the_signature_requirement(self):
        self.assertEqual(nrlog.validate_playbook(
            self.fields(signatures=[], stated_differs_from_observed='true'), 'x.md'), [])

    def test_a_false_condition_flag_does_not_satisfy_it(self):
        errors = nrlog.validate_playbook(
            self.fields(signatures=[], stated_differs_from_observed='false'), 'x.md')
        self.assertTrue(any('stated_differs_from_observed' in e for e in errors))

    def test_a_non_boolean_condition_flag_is_reported(self):
        errors = nrlog.validate_playbook(
            self.fields(stated_differs_from_observed='sometimes'), 'x.md')
        self.assertTrue(any('boolean' in e for e in errors))

    def test_unknown_level_max_is_reported(self):
        errors = nrlog.validate_playbook(
            self.fields(signatures=[], level_max='banana'), 'x.md')
        self.assertTrue(any('level_max' in e for e in errors))


class LevelOrderTests(unittest.TestCase):

    def test_finest_satisfies_info(self):
        self.assertTrue(nrlog.level_at_least('FINEST', 'info'))

    def test_info_does_not_satisfy_finest(self):
        self.assertFalse(nrlog.level_at_least('INFO', 'finest'))

    def test_aliases_map_to_finest(self):
        self.assertTrue(nrlog.level_at_least('ALL', 'finest'))
        self.assertTrue(nrlog.level_at_least('VERBOSE', 'debug'))

    def test_unknown_level_is_treated_as_info(self):
        self.assertTrue(nrlog.level_at_least('BANANA', 'info'))
        self.assertFalse(nrlog.level_at_least('BANANA', 'debug'))

    def test_level_at_most_bounds_the_other_way(self):
        self.assertTrue(nrlog.level_at_most('INFO', 'info'))
        self.assertTrue(nrlog.level_at_most('WARN', 'info'))
        self.assertFalse(nrlog.level_at_most('DEBUG', 'info'))

    def test_known_level_separates_aliases_from_nonsense(self):
        self.assertTrue(nrlog.known_level('all'))
        self.assertTrue(nrlog.known_level('FINEST:'))
        self.assertFalse(nrlog.known_level('banana'))
        self.assertFalse(nrlog.known_level(None))


class LoadPlaybooksTests(unittest.TestCase):

    def test_loads_the_shipped_set_and_all_validate(self):
        books = nrlog.load_playbooks()
        self.assertGreaterEqual(len(books), 9)
        ids = [b.id for b in books]
        self.assertEqual(len(ids), len(set(ids)), 'playbook ids must be unique')
        for book in books:
            self.assertTrue(book.signatures or book.level_max
                            or book.stated_differs_from_observed,
                            '%s has no signature and no condition' % book.path)
            self.assertIn('## Customer fix', book.body, book.path)
            self.assertIn('## Next ask', book.body, book.path)

    def test_sorted_by_precedence_then_id(self):
        books = nrlog.load_playbooks()
        keys = [(b.precedence, b.id) for b in books]
        self.assertEqual(keys, sorted(keys))

    def test_log_level_playbook_sorts_first(self):
        books = nrlog.load_playbooks()
        self.assertEqual(books[0].id, 7, 'playbook 7 bounds every other verdict')

    def test_no_signature_carries_a_placeholder_or_regex(self):
        for book in nrlog.load_playbooks():
            for signature in book.signatures:
                self.assertFalse(nrlog.looks_like_regex(signature),
                                 '%s: %r' % (book.path, signature))

    def test_missing_directory_exits(self):
        with self.assertRaises(SystemExit):
            nrlog.load_playbooks(os.path.join(HERE, 'no-such-playbook-dir'))

    def test_section_returns_the_named_block(self):
        book = nrlog.load_playbooks()[0]
        self.assertTrue(book.section('Customer fix'))
        self.assertIsNone(book.section('Nonexistent heading'))


FIXTURES = os.path.join(HERE, 'fixtures', 'logs')


def load_session(name, number=None):
    paths = nrlog.collect_managed(FIXTURES, name)
    sessions = nrlog.build_sessions(paths)
    return sessions[(number or 1) - 1]


class MatcherTests(unittest.TestCase):

    def setUp(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        self.books = nrlog.load_playbooks()

    def test_every_playbook_gets_exactly_one_state(self):
        session = load_session('Quiet')
        results = nrlog.match_playbooks(session, self.books)
        self.assertEqual(len(results), len(self.books))
        for result in results:
            self.assertIn(result.state, ('matched', 'blocked', 'clear'))

    def test_results_come_back_in_the_playbooks_own_order(self):
        session = load_session('Quiet')
        results = nrlog.match_playbooks(session, self.books)
        self.assertEqual([r.playbook.id for r in results], [b.id for b in self.books])

    def test_finest_playbook_is_blocked_on_an_info_session(self):
        session = load_session('Quiet')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[8].state, 'blocked')
        self.assertEqual(results[8].hits, 0)

    def test_level_max_playbook_matches_an_info_session_with_no_signature(self):
        session = load_session('Quiet')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[7].state, 'matched')
        self.assertEqual(results[7].hits, 0)
        self.assertEqual(results[7].reason, 'level')

    def test_level_max_playbook_is_clear_on_a_finest_session(self):
        session = load_session('MyApp', 2)
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[7].state, 'clear')

    def test_a_managed_signature_matches_and_carries_its_evidence(self):
        session = load_session('Quiet')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[3].state, 'matched')
        self.assertEqual(results[3].hits, 1)
        name, lineno, text = results[3].evidence[0]
        self.assertEqual(name, 'newrelic_agent_Quiet.log')
        self.assertEqual(lineno, 4)
        self.assertIn('401 Unauthorized', text)

    def test_evidence_is_capped(self):
        session = load_session('Quiet')
        results = nrlog.match_playbooks(session, self.books, max_evidence=2, width=40)
        for result in results:
            self.assertLessEqual(len(result.evidence), 2)
            for _name, _lineno, text in result.evidence:
                self.assertLessEqual(len(text), 40)

    def test_evidence_is_redacted(self):
        session = load_session('Quiet')
        results = nrlog.match_playbooks(session, self.books)
        keyed = [text for result in results
                 for _name, _lineno, text in result.evidence if 'license_key' in text]
        self.assertTrue(keyed, 'the Quiet fixture must plant a key inside matched evidence')
        for text in keyed:
            self.assertNotRegex(text, r'[0-9a-zA-Z]{36}NRAL')
            self.assertIn('[REDACTED]', text)

    def test_lines_from_another_pid_are_not_evidence(self):
        session = load_session('MyApp', 3)
        self.assertEqual(session.pid, 9100)
        results = {r.playbook.id: r
                   for r in nrlog.match_playbooks(session, self.books, max_evidence=10)}
        texts = ' '.join(t for _n, _l, t in results[3].evidence)
        self.assertNotIn('collector-inside', texts,
                         'the pid 8412 line inside this range belongs to another session')
        self.assertEqual(results[3].hits, 0, 'pid 9100 owns none of the 8412 lines')
        self.assertEqual(results[8].hits, 0)

    def test_lines_outside_the_session_range_are_not_evidence(self):
        session = load_session('MyApp', 2)
        results = {r.playbook.id: r
                   for r in nrlog.match_playbooks(session, self.books, max_evidence=10)}
        texts = ' '.join(t for _n, _l, t in results[3].evidence)
        self.assertIn('collector-inside', texts, 'this session owns the 14:03:14 line')
        self.assertNotIn('collector-later', texts,
                         'the 15:30 restart is a different session of the same pid')

    def test_a_continuation_line_follows_its_owner_pid(self):
        owner = load_session('MyApp', 2)
        other = load_session('MyApp', 3)
        mine = {r.playbook.id: r
                for r in nrlog.match_playbooks(owner, self.books, max_evidence=10)}
        theirs = {r.playbook.id: r
                  for r in nrlog.match_playbooks(other, self.books, max_evidence=10)}
        self.assertIn('Check your proxy settings',
                      ' '.join(t for _n, _l, t in mine[3].evidence),
                      'the continuation line stays with the pid that logged it')
        self.assertNotIn('Check your proxy settings',
                         ' '.join(t for _n, _l, t in theirs[3].evidence))

    def test_evidence_is_capped_at_two_lines(self):
        session = load_session('MyApp', 2)
        paths = [os.path.join(FIXTURES, 'NewRelic.Profiler.8412.log')]
        results = {r.playbook.id: r for r in
                   nrlog.match_playbooks(session, self.books, profiler_paths=paths,
                                         max_evidence=2)}
        self.assertGreater(results[4].hits, 2, 'this fixture must produce more hits '
                                              'than the cap or the cap is untested')
        self.assertEqual(len(results[4].evidence), 2)

    def test_one_line_counts_as_one_hit_for_one_playbook(self):
        session = load_session('Quiet')
        two = nrlog.Playbook(
            {'id': 998, 'title': 'two signatures on one line', 'tier': 'verified',
             'scope': 'managed', 'min_level': 'info', 'precedence': 98,
             'signatures': ['Agent fully connected', 'Application name(s)'],
             'keywords': [], 'verified_versions': '10.54.0'},
            '## Customer fix\nx\n\n## Next ask\nx\n', 'two.md')
        result = nrlog.match_playbooks(session, [two])[0]
        self.assertEqual(result.state, 'matched')
        self.assertEqual(result.hits, 1, 'one line is one hit, not one per signature')
        self.assertEqual(len(result.evidence), 1)

    def test_scope_keeps_each_playbook_in_its_own_log(self):
        session = load_session('Quiet')
        paths = [os.path.join(FIXTURES, 'NewRelic.Profiler.7777.log')]
        managed_book = nrlog.Playbook(
            {'id': 997, 'title': 'managed scope, profiler string', 'tier': 'verified',
             'scope': 'managed', 'min_level': 'info', 'precedence': 97,
             'signatures': ['is not configured to be instrumented'], 'keywords': [],
             'verified_versions': '10.54.0'},
            '## Customer fix\nx\n\n## Next ask\nx\n', 'managed.md')
        profiler_book = nrlog.Playbook(
            {'id': 996, 'title': 'profiler scope, managed string', 'tier': 'verified',
             'scope': 'profiler', 'min_level': 'info', 'precedence': 96,
             'signatures': ['Agent fully connected'], 'keywords': [],
             'verified_versions': '10.54.0'},
            '## Customer fix\nx\n\n## Next ask\nx\n', 'profiler.md')
        results = {r.playbook.id: r for r in
                   nrlog.match_playbooks(session, [managed_book, profiler_book],
                                         profiler_paths=paths)}
        self.assertEqual(results[997].hits, 0,
                         'a managed playbook must not read the profiler log')
        self.assertEqual(results[997].evidence, [])
        self.assertEqual(results[996].hits, 0,
                         'a profiler playbook must not read the managed log')
        self.assertEqual(results[996].evidence, [])

    def test_a_hit_beats_the_level_gate(self):
        book = self.books[0]
        session = load_session('Quiet')
        forced = nrlog.Playbook(
            {'id': 999, 'title': 'forced', 'tier': 'verified', 'scope': 'managed',
             'min_level': 'finest', 'precedence': 99,
             'signatures': ['Agent fully connected'], 'keywords': [],
             'verified_versions': '10.54.0'},
            '## Customer fix\nx\n\n## Next ask\nx\n', 'forced.md')
        results = {r.playbook.id: r for r in
                   nrlog.match_playbooks(session, [book, forced])}
        self.assertEqual(results[999].state, 'matched',
                         'evidence outranks the stated level')
        self.assertEqual(results[999].reason, 'signature')

    def test_profiler_scope_playbook_matches_a_profiler_log(self):
        session = load_session('Quiet')
        paths = [os.path.join(FIXTURES, 'NewRelic.Profiler.7777.log')]
        results = {r.playbook.id: r
                   for r in nrlog.match_playbooks(session, self.books, profiler_paths=paths)}
        self.assertEqual(results[2].state, 'matched')
        self.assertGreaterEqual(results[2].hits, 1)

    def test_profiler_scope_playbook_is_never_blocked(self):
        session = nrlog.Session(4242, datetime(2026, 8, 19, 9, 0, 0),
                                os.path.join(FIXTURES, 'newrelic_agent_Quiet.log'))
        session.level_counts = {'WARN': 3}
        self.assertEqual(nrlog.session_observed_level(session), 'WARN')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        for book_id in (1, 2, 4):
            self.assertEqual(results[book_id].state, 'clear',
                             'a profiler playbook has no managed level gate')
        self.assertEqual(results[8].state, 'blocked',
                         'a managed playbook below its min_level is blocked here')

    def test_level_change_playbook_ignores_the_config_set_startup_line(self):
        session = load_session('ConfigDebug')
        self.assertEqual(session.log_level, 'DEBUG')
        self.assertEqual(nrlog.session_observed_level(session), 'DEBUG')
        self.assertTrue(session.level_changes,
                        'this fixture must carry the startup level-change line')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[9].state, 'clear',
                         'a config-set level writes that line at startup and is benign')

    def test_level_change_playbook_matches_a_stated_observed_disagreement(self):
        session = load_session('Shared')
        self.assertEqual(session.log_level, 'INFO')
        self.assertEqual(nrlog.session_observed_level(session), 'FINEST')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[9].state, 'matched')
        self.assertEqual(results[9].reason, 'level-change')
        self.assertEqual(results[9].hits, 0)

    def test_level_change_playbook_ignores_a_session_quieter_than_stated(self):
        session = nrlog.Session(4242, datetime(2026, 8, 19, 9, 0, 0),
                                os.path.join(FIXTURES, 'newrelic_agent_Quiet.log'))
        session.log_level = 'FINEST'
        session.level_counts = {'INFO': 10, 'DEBUG': 4}
        self.assertEqual(nrlog.session_observed_level(session), 'DEBUG')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[9].state, 'clear',
                         'quieter than stated is not runaway log volume')

    def test_level_change_playbook_is_blocked_without_a_startup_banner(self):
        session = load_session('MyApp', 1)
        self.assertIsNone(session.log_level)
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[9].state, 'blocked')
        self.assertEqual(results[9].reason, 'no-stated-level')

    def test_match_state_exists_before_the_matcher_runs(self):
        self.assertIsNone(nrlog.Match(self.books[0]).state)

    def test_a_session_with_no_level_counts_blocks_every_managed_playbook(self):
        session = nrlog.Session(4242, datetime(2026, 8, 19, 9, 0, 0),
                                os.path.join(FIXTURES, 'newrelic_agent_Quiet.log'))
        self.assertEqual(nrlog.session_observed_level(session), 'OFF')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[8].state, 'blocked')
        self.assertEqual(results[3].state, 'blocked')
        self.assertEqual(results[1].state, 'clear', 'profiler scope has no level gate')

    def test_observed_level_is_the_most_verbose_present(self):
        self.assertEqual(nrlog.session_observed_level(load_session('Quiet')), 'INFO')
        self.assertEqual(nrlog.session_observed_level(load_session('MyApp', 2)), 'FINEST')

    def test_a_disabled_wrapper_matches_playbook_ten(self):
        session = load_session('WrapperDisabled')
        results = {r.playbook.id: r for r in nrlog.match_playbooks(session, self.books)}
        self.assertEqual(results[10].state, 'matched')
        self.assertEqual(results[10].reason, 'signature')
        self.assertEqual(results[10].hits, 1)
        _name, _lineno, text = results[10].evidence[0]
        self.assertIn('due to too many consecutive exceptions', text)


class KeywordPatternTests(unittest.TestCase):
    """Pins _keyword_pattern's edge behaviour. Do not change the pattern for these: all 27
    shipped keywords start and end with a letter, so no shipped keyword hits this edge."""

    def test_a_leading_non_word_character_still_needs_a_boundary_before_it(self):
        pattern = nrlog._keyword_pattern('-timeout')
        self.assertTrue(pattern.search('a -timeout occurred'))
        self.assertTrue(pattern.search('-timeout occurred'))
        self.assertFalse(pattern.search('x-timeout occurred'),
                         'a word character right before the leading non-word character '
                         'is itself treated as the boundary, so it blocks the match')

    def test_a_trailing_non_word_character_still_needs_a_boundary_after_it(self):
        pattern = nrlog._keyword_pattern('timeout-')
        self.assertTrue(pattern.search('timeout- happened'))
        self.assertTrue(pattern.search('a timeout- '))
        self.assertFalse(pattern.search('timeout-x happened'),
                         'a word character right after the trailing non-word character '
                         'is itself treated as the boundary, so it blocks the match')


class ProfilerCorrelationTests(unittest.TestCase):

    def setUp(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')

    def test_pid_must_also_overlap_in_time(self):
        session = load_session('Quiet')
        self.assertEqual(session.pid, 5150)
        self.assertTrue(os.path.isfile(os.path.join(FIXTURES, 'NewRelic.Profiler.5150.log')),
                        'the reused-pid fixture must exist or this test proves nothing')
        self.assertEqual(nrlog.profiler_logs_for_session(session, FIXTURES), [],
                         'pid 5150 matches but the UTC range does not')

    def test_an_overlapping_pid_is_found(self):
        session = load_session('MyApp', 2)
        self.assertEqual(session.pid, 8412)
        found = nrlog.profiler_logs_for_session(session, FIXTURES)
        self.assertEqual([os.path.basename(p) for p in found],
                         ['NewRelic.Profiler.8412.log'])

    def test_no_match_returns_empty_rather_than_guessing(self):
        session = load_session('Quiet')
        self.assertEqual(nrlog.profiler_logs_for_session(session, os.path.join(HERE)), [])

    def test_a_profiler_log_is_read_once_per_run(self):
        path = os.path.join(FIXTURES, 'NewRelic.Profiler.8412.log')
        nrlog.reset_profiler_cache()
        calls = []
        real = nrlog.read_profiler

        def counting(target):
            calls.append(target)
            return real(target)

        nrlog.read_profiler = counting
        try:
            session = load_session('MyApp', 2)
            found = nrlog.profiler_logs_for_session(session, FIXTURES)
            nrlog.match_playbooks(session, nrlog.load_playbooks(), profiler_paths=found)
        finally:
            nrlog.read_profiler = real
            nrlog.reset_profiler_cache()
        self.assertEqual(calls.count(path), 1,
                         'the correlated profiler log must be parsed once, not twice')


PLAYBOOK_FIXTURES = os.path.join(HERE, 'fixtures', 'playbooks')
MULTI_FIXTURES = os.path.join(HERE, 'fixtures', 'multi')
GOLDEN = os.path.join(HERE, 'golden')
CHANGELOG = os.path.normpath(os.path.join(HERE, '..', '..', '..', '..',
                                          'src', 'Agent', 'CHANGELOG.md'))


def normalize(text):
    """Replace the machine path, the data export date, and the playbook inventory, so a
    golden file is portable across things that legitimately vary rather than the report's
    shape. The MATCHED numerator (how many matched) stays pinned; only the denominator (how
    many were loaded) is inventory.
    """
    text = text.replace(FIXTURES, '<FIXTURES>')
    text = re.sub(r'exported \d{4}-\d{2}-\d{2}', 'exported <DATA-DATE>', text)
    text = re.sub(r'playbooks: \d+ \(', 'playbooks: <N> (', text)
    text = re.sub(r'(MATCHED\s+\d+ of) \d+( playbook\(s\))', r'\1 <N>\2', text)
    return re.sub(r'(CLEAR\s+playbooks) [\d, ]+( did not match)', r'\1 <IDS>\2', text)


def _log_line(ts, level, pid, tid, msg):
    """Build one raw managed-log line in the format nrlog.py's MANAGED_RE parses."""
    return '%s NewRelic %6s: [pid: %d, tid: %d] %s\n' % (ts, level, pid, tid, msg)


def matched_labels(lines):
    """The [id] label lines inside the MATCHED block, and no other block's."""
    found = []
    inside = False
    for text in lines:
        if text.startswith('MATCHED'):
            inside = True
            continue
        if inside and text[:9].strip():
            break
        if inside and re.match(r'\s+\[\d+\]', text):
            found.append(text)
    return found


class TriageTests(unittest.TestCase):

    def setUp(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        nrlog.reset_profiler_cache()

    def report(self, **kwargs):
        options = dict(path=FIXTURES, file='Quiet', session=None, playbooks=None,
                       max_matched=5, evidence=2, width=200, no_version=True)
        options.update(kwargs)
        return nrlog.triage_report(**options)

    def playbooks(self, name):
        if not os.path.isdir(PLAYBOOK_FIXTURES):
            self.skipTest('run python tests/make_playbook_fixtures.py first')
        return os.path.join(PLAYBOOK_FIXTURES, name)

    def test_report_has_every_required_block(self):
        text = '\n'.join(self.report())
        for label in ('INPUT', 'SESSION', 'PROFILER', 'MATCHED', 'BLOCKED', 'CLEAR',
                      'ERRORS', 'NEXT'):
            self.assertIn(label, text)

    def test_header_stamps_the_playbooks_provenance_even_without_the_version_block(self):
        header = self.report()[0]
        self.assertIn('playbooks verified against 10.54.0', header)
        self.assertNotIn('VERSION', '\n'.join(self.report()))

    def test_blocked_names_the_level_it_needs(self):
        text = '\n'.join(self.report())
        self.assertRegex(text, r'BLOCKED.*\[8\].*FINEST')

    def test_clear_enumerates_the_ids_for_the_shipped_set(self):
        text = '\n'.join(self.report())
        self.assertRegex(text, r'CLEAR\s+playbooks 1, 2, 4, 5, 6, 9, 10 did not match')

    def test_clear_collapses_to_a_count_at_scale(self):
        text = '\n'.join(self.report(playbooks=self.playbooks('scale')))
        self.assertRegex(text, r'CLEAR\s+40 playbook')

    def test_matched_list_is_capped(self):
        lines = self.report(max_matched=1)
        labels = matched_labels(lines)
        self.assertEqual(len(labels), 1)
        self.assertRegex(labels[0], r'\[3\]', 'selection is by hits, not by precedence')
        self.assertIn('... 1 more matched, not shown', '\n'.join(lines))

    def test_matched_list_is_capped_at_scale(self):
        lines = self.report(playbooks=self.playbooks('scale-match'))
        self.assertEqual(len(matched_labels(lines)), 5)
        self.assertIn('... 35 more matched, not shown', '\n'.join(lines))

    def test_matched_display_order_is_by_precedence_not_hits(self):
        labels = matched_labels(self.report())
        self.assertRegex(labels[0], r'\[7\]', 'playbook 7 has the lowest precedence')
        self.assertRegex(labels[1], r'\[3\]')

    def test_playbook_ten_outranks_playbook_eight_when_both_match(self):
        labels = matched_labels(self.report(file='WrapperDisabled'))
        self.assertRegex(labels[0], r'\[10\]',
                         'precedence 45 must display before playbook 8 at precedence 60')
        self.assertRegex(labels[1], r'\[8\]')

    def test_a_level_condition_match_renders_the_observed_level_not_an_evidence_line(self):
        text = '\n'.join(self.report())
        self.assertIn('observed level INFO is at or below the info ceiling', text)
        self.assertNotIn('[7] Log level too low to diagnose             0 hit(s)', text)

    def test_a_signature_match_renders_its_redacted_evidence(self):
        text = '\n'.join(self.report())
        self.assertIn('newrelic_agent_Quiet.log:4', text)
        self.assertIn('[REDACTED]', text)
        self.assertNotIn('abcdefghij0123456789klmnopqrstuvwxyzNRAL', text)

    def test_matched_block_carries_a_candidate_not_conclusion_caution(self):
        text = '\n'.join(self.report())
        self.assertIn('a signature hit is a candidate, not a conclusion', text)

    def test_matched_block_has_no_caution_when_nothing_matched(self):
        """With zero playbooks loaded, nothing can match, so MATCHED renders 'none'
        and the caution line (which belongs to the non-empty branch) is absent."""
        empty_playbooks = tempfile.mkdtemp()
        try:
            lines = self.report(playbooks=empty_playbooks)
            text = '\n'.join(lines)
            self.assertRegex(text, r'MATCHED\s+none')
            self.assertNotIn('a signature hit is a candidate', text)
        finally:
            shutil.rmtree(empty_playbooks, ignore_errors=True)

    def test_errors_block_reports_the_honest_empty_case_when_nothing_is_unaccounted(self):
        """Quiet's one ERROR line is already quoted as evidence for playbook 3, so
        nothing is left for the ERRORS block to show."""
        text = '\n'.join(self.report())
        self.assertIn('ERRORS    no ERROR or WARN lines in this session', text)

    def test_a_level_change_match_states_the_direction(self):
        lines = nrlog.triage_report(path=FIXTURES, file='Shared', session=None,
                                    playbooks=None, max_matched=5, evidence=2,
                                    width=200, no_version=True)
        text = '\n'.join(lines)
        self.assertRegex(text, r'\[9\] Runaway log volume')
        self.assertIn('observed FINEST is more verbose than the stated INFO', text)
        self.assertIn('level change 2026-08-18 16:10:00.000 INFO -> FINEST', text)

    def test_a_missing_startup_banner_blocks_the_level_change_playbook(self):
        lines = nrlog.triage_report(path=os.path.join(FIXTURES,
                                                      'newrelic_agent_MyApp_001.log'),
                                    file=None, session=None, playbooks=None,
                                    max_matched=5, evidence=2, width=200, no_version=True)
        text = '\n'.join(lines)
        self.assertRegex(text, r'BLOCKED\s+\[9\].*no startup banner')

    def test_profiler_block_groups_the_files_and_points_at_a_playbook(self):
        text = '\n'.join(self.report())
        self.assertRegex(text, r'PROFILER\s+3 file\(s\), 3 group\(s\), 0 correlated')
        self.assertRegex(text, r'process-rejected.*expected noise')
        self.assertRegex(text, r'process-rejected.*playbook 2')
        self.assertIn('no profiler log matches this pid and time range', text)

    def test_field_tier_is_labelled(self):
        text = '\n'.join(self.report(playbooks=self.playbooks('field')))
        self.assertIn('[field]', text)

    def test_multiple_managed_logs_refuse_to_choose(self):
        if not os.path.isdir(MULTI_FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        lines = nrlog.triage_report(
            path=MULTI_FIXTURES, file=None, session=None,
            playbooks=None, max_matched=5, evidence=2, width=200, no_version=True)
        text = '\n'.join(lines)
        self.assertIn('--file', text)
        self.assertNotIn('MATCHED', text)
        self.assertIn('newrelic_agent_AppOne.log', text)
        self.assertIn('newrelic_agent_AppTwo.log', text)

    def test_other_sessions_are_listed_with_their_numbers(self):
        text = '\n'.join(self.report(file='MyApp'))
        self.assertRegex(text, r'other sessions: .*rerun with --session N')

    def test_session_out_of_range_exits(self):
        with self.assertRaises(SystemExit):
            self.report(session=99)

    def test_session_zero_exits_instead_of_silently_picking_the_newest(self):
        with self.assertRaises(SystemExit):
            self.report(session=0)

    def test_output_matches_the_golden_file(self):
        path = os.path.join(GOLDEN, 'triage-basic.txt')
        self.assertTrue(os.path.isfile(path),
                        'golden file missing; re-record it with '
                        'python tests/record_golden.py')
        with open(path, 'r', encoding='utf-8') as handle:
            expected = handle.read().rstrip('\n')
        self.assertEqual(normalize('\n'.join(self.report())), normalize(expected),
                         'triage output shape changed; re-record the golden file '
                         'only if the change is intended')

    def test_reset_profiler_cache_lets_a_second_call_see_new_profiler_content(self):
        """Minor 2 / R12: reset_profiler_cache() at the top of triage_report is required.
        Without it, the module-level _PROFILER_CACHE keyed by path would still hold the
        first call's parse of this same path, and the second call's MATCHED block would
        stay 'clear' instead of picking up the newly written reject lines."""
        scratch = tempfile.mkdtemp()
        try:
            managed_path = os.path.join(scratch, 'newrelic_agent_Scratch.log')
            with open(managed_path, 'w', encoding='utf-8') as handle:
                handle.write(
                    "2026-08-20 09:00:00,000 NewRelic   INFO: [pid: 4242, tid: 1] "
                    "The New Relic .NET Agent v10.44.0 started (pid 4242) on app domain "
                    "'/LM/W3SVC/1/ROOT'\n")
            profiler_path = os.path.join(scratch, 'NewRelic.Profiler.4242.log')

            def run():
                return '\n'.join(nrlog.triage_report(
                    path=scratch, file=None, session=None, playbooks=None,
                    max_matched=5, evidence=2, width=200, no_version=True))

            with open(profiler_path, 'w', encoding='utf-8') as handle:
                handle.write('[Info ] 2026-08-20 09:00:00 Profiler initialized\n')
            first = run()
            self.assertNotIn('is not configured to be instrumented', first)

            with open(profiler_path, 'w', encoding='utf-8') as handle:
                handle.write('[Info ] 2026-08-20 09:00:01 This process (C:\\Apps\\Scratch.exe) '
                             'is not configured to be instrumented.\n')
                handle.write('[Info ] 2026-08-20 09:00:02 This process should not be '
                             'instrumented, unloading profiler.\n')
            second = run()
            self.assertIn('is not configured to be instrumented', second,
                         'the second call must re-read the profiler log, not reuse the '
                         'first call\'s cached parse of the same path')
        finally:
            shutil.rmtree(scratch, ignore_errors=True)


class ErrorsBlockTests(unittest.TestCase):
    """The ERRORS block: what the report shows when no matched playbook already
    quoted an ERROR or WARN line as its evidence."""

    KEY = 'abcdefghij0123456789klmnopqrstuvwxyz' + 'NRAL'

    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.mkdtemp()
        path = os.path.join(cls.scratch, 'newrelic_agent_Errs.log')
        with open(path, 'w', encoding='utf-8') as handle:
            handle.writelines([
                _log_line('2026-08-21 10:00:00,000', 'INFO', 6001, 1,
                          "The New Relic .NET Agent v10.44.0 started (pid 6001) on app "
                          "domain '/Errs'"),
                _log_line('2026-08-21 10:00:00,010', 'INFO', 6001, 1,
                          'Log level set to INFO'),
                _log_line('2026-08-21 10:00:01,000', 'ERROR', 6001, 1,
                          'Received a 401 Unauthorized response invoking method '
                          '"connect" with payload "..."'),
                _log_line('2026-08-21 10:00:02,000', 'ERROR', 6001, 2,
                          'Unhandled exception in custom instrumentation for method '
                          '"Foo.Bar.Baz"'),
                _log_line('2026-08-21 10:00:03,000', 'ERROR', 6001, 2,
                          'Unhandled exception in custom instrumentation for method '
                          '"Foo.Qux.Quux"'),
                _log_line('2026-08-21 10:00:04,000', 'ERROR', 6001, 2,
                          'Unhandled exception in custom instrumentation for method '
                          '"Foo.Corge.Grault"'),
                _log_line('2026-08-21 10:00:05,000', 'WARN', 6001, 1,
                          'Configured license key appears malformed: ' + cls.KEY),
                _log_line('2026-08-21 10:00:10,000', 'INFO', 6001, 1,
                          "The New Relic .NET Agent v10.44.0 has shutdown (pid 6001) on "
                          "app domain '/Errs'"),
            ])
        nrlog.reset_profiler_cache()
        cls.report_lines = nrlog.triage_report(path=cls.scratch, file=None, session=None,
                                               playbooks=None, max_matched=5, evidence=2,
                                               width=200, no_version=True)
        cls.text = '\n'.join(cls.report_lines)

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.scratch, ignore_errors=True)
        nrlog.reset_profiler_cache()

    def _errors_block(self):
        block = []
        inside = False
        for text in self.report_lines:
            if text.startswith('ERRORS'):
                inside = True
            elif inside and text[:9].strip():
                break
            if inside:
                block.append(text)
        return '\n'.join(block)

    def test_unattributed_error_lines_appear_in_the_block(self):
        self.assertIn('Unhandled exception in custom instrumentation',
                      self._errors_block())

    def test_a_line_already_quoted_as_matched_evidence_does_not_repeat(self):
        self.assertIn('Received a 401 Unauthorized', self.text)
        self.assertNotIn('Received a 401 Unauthorized', self._errors_block())

    def test_three_lines_differing_only_by_method_name_collapse_to_one_group(self):
        block = self._errors_block()
        self.assertEqual(block.count('Unhandled exception in custom instrumentation'), 1)
        self.assertIn('seen 3 time(s)', block)

    def test_a_secret_shaped_string_in_an_error_line_is_redacted(self):
        block = self._errors_block()
        self.assertIn('[REDACTED]', block)
        self.assertNotIn(self.KEY, block)


class ProfilerSignatureGuardTests(unittest.TestCase):
    """The roster's literals and the playbook signatures must not drift apart."""

    def setUp(self):
        self.books = nrlog.load_playbooks()

    def test_every_fault_literal_is_covered_by_a_playbook_signature(self):
        signatures = [s for book in self.books for s in book.signatures]
        for name, needle in nrlog.PROFILER_SIGNATURES:
            if name in nrlog.PROFILER_HEALTHY_GROUPS:
                continue
            self.assertTrue(
                any(nrlog.signature_relates(needle, s) for s in signatures),
                '%s: PROFILER_SIGNATURES literal %r has no related playbook '
                'signature; one of the two lists is stale' % (name, needle))

    def test_the_healthy_exemption_list_names_only_real_groups(self):
        names = [name for name, _needle in nrlog.PROFILER_SIGNATURES]
        for name in nrlog.PROFILER_HEALTHY_GROUPS + nrlog.PROFILER_NOISE_GROUPS:
            self.assertIn(name, names)

    def test_a_roster_group_resolves_to_its_playbook_ids(self):
        self.assertEqual(nrlog.roster_playbook_ids(('process-rejected',), self.books), [2])
        self.assertEqual(nrlog.roster_playbook_ids(('clr-init-failed',), self.books), [1])
        self.assertEqual(nrlog.roster_playbook_ids(('initialized',), self.books), [])


class VersionTests(unittest.TestCase):

    DATA = {
        'source_label': 'repo checkout',
        'latest': '10.54.0',
        'releases': [
            {'version': '10.54.0', 'date': '2026-08-25',
             'fixes': ['Release held WCF client transaction when async result completes (#3772)']},
            {'version': '10.53.5', 'date': '2026-08-20',
             'fixes': ['Send loaded modules on agent reconnect (#3534)',
                       "Don't instrument Blazor / SignalR websocket connections (#3468)",
                       'Fix agent connection error when debug logging is enabled (#3395)',
                       'MSSQL connection string parsing can throw exceptions and disable '
                       'Datastore instrumentation (#3179)',
                       'Improve logging and validation for license keys (#2982)']},
            {'version': '10.53.1', 'date': '2026-08-12',
             'fixes': ['Guard null OperationContext in WCF MethodInvokerWrapper (#3730)',
                       'Prevent double RUM script injection (#3726)']},
            {'version': '10.40.1', 'date': '2026-01-05', 'fixes': ['Older fix (#1)']},
        ],
    }

    def test_parse_version(self):
        self.assertEqual(nrlog.parse_version('10.40.1'), (10, 40, 1))
        self.assertEqual(nrlog.parse_version('v10.40.1'), (10, 40, 1))
        self.assertIsNone(nrlog.parse_version('unknown'))

    def test_parse_version_of_a_four_part_agent_version(self):
        self.assertEqual(nrlog.parse_version('10.53.0.0'), (10, 53, 0))

    def test_counts_releases_behind(self):
        text = '\n'.join(nrlog.version_block('10.40.1', [], self.DATA))
        self.assertIn('10.54.0', text)
        self.assertIn('3 release(s) behind', text)

    def test_filters_fixes_by_keyword(self):
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf'], self.DATA))
        self.assertIn('WCF', text)
        self.assertNotIn('RUM', text)

    def test_keyword_filter_excludes_substring_matches(self):
        text = '\n'.join(nrlog.version_block(
            '10.40.1', ['connect', 'license key', 'proxy', 'tls'], self.DATA))
        self.assertNotIn('reconnect', text)
        self.assertNotIn('Blazor', text)
        self.assertNotIn('debug logging', text)
        self.assertNotIn('MSSQL', text)

    def test_keyword_filter_tolerates_a_plural_suffix(self):
        text = '\n'.join(nrlog.version_block(
            '10.40.1', ['connect', 'license key', 'proxy', 'tls'], self.DATA))
        self.assertIn('license keys', text)

    def test_no_keywords_shows_candidates_not_nothing(self):
        text = '\n'.join(nrlog.version_block('10.40.1', [], self.DATA))
        self.assertIn('#3772', text)
        self.assertIn('no playbook matched, so no fix filter was applied', text)
        self.assertIn('candidates, not findings', text)

    def test_unfiltered_total_exceeds_matched_count(self):
        text = '\n'.join(nrlog.version_block(
            '10.40.1', ['wrapper', 'transaction', 'segment'], self.DATA))
        self.assertIn('8 fix(es) total across those releases, before any filter', text)
        self.assertIn('fixes since, matching this symptom: 1', text)

    def test_filter_scope_is_stated(self):
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf'], self.DATA))
        self.assertIn("this filter uses only the matched playbooks' keywords", text)

    def test_the_3263_regression_surfaces_through_the_unfiltered_path(self):
        # Live ticket: the changelog held the entry for #3263, but it shares
        # none of playbook 8's keywords, so the old filter hid it. Fix 1b
        # makes the unfiltered path (keywords=[]) show it instead.
        data = {
            'source_label': 'repo checkout',
            'latest': '10.45.0',
            'releases': [
                {'version': '10.45.0', 'date': '2026-03-01',
                 'fixes': ['MSSQL connection string parsing can throw exceptions and '
                           'disable Datastore instrumentation (#3263)']},
            ],
        }
        playbook_8_keywords = ['wrapper', 'transaction', 'segment']
        filtered = '\n'.join(nrlog.version_block('10.40.1', playbook_8_keywords, data))
        self.assertNotIn('#3263', filtered)
        unfiltered = '\n'.join(nrlog.version_block('10.40.1', [], data))
        self.assertIn('#3263', unfiltered)

    def test_current_version_says_so(self):
        text = '\n'.join(nrlog.version_block('10.54.0', ['wcf'], self.DATA))
        self.assertIn('current', text)

    def test_unknown_version_is_stated_not_guessed(self):
        text = '\n'.join(nrlog.version_block(None, ['wcf'], self.DATA))
        self.assertIn('not stated', text)

    def test_missing_data_file_is_not_fatal(self):
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf'], None))
        self.assertIn('changelog not reachable', text)
        self.assertIn('currency not checked', text)

    def test_a_keyword_match_is_labelled_a_candidate(self):
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf'], self.DATA))
        self.assertIn('a keyword match is not a diagnosis', text)

    def test_the_fix_list_is_capped(self):
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf', 'rum'], self.DATA, limit=1))
        self.assertIn('... 2 more', text)
        self.assertIn('could outrank what is shown above', text)

    def test_worked_on_lists_every_entry_in_the_window_unfiltered(self):
        # window (10.40.1, 10.53.5]: the 10.53.1 and 10.53.5 releases, 7 fixes total,
        # including the reconnect fix that the 'wcf' keyword would never have matched
        text = '\n'.join(nrlog.version_block('10.53.5', ['wcf'], self.DATA,
                                             worked_on='10.40.1'))
        self.assertIn('WORKED-ON', text)
        self.assertIn('window 10.40.1 .. 10.53.5', text)
        self.assertIn('7 entrie(s), complete and unfiltered', text)
        self.assertIn('Send loaded modules on agent reconnect', text)
        self.assertIn('Guard null OperationContext in WCF MethodInvokerWrapper', text)

    def test_worked_on_at_or_newer_than_session_renders_no_window_message(self):
        at_version = '\n'.join(nrlog.version_block('10.40.1', [], self.DATA,
                                                    worked_on='10.40.1'))
        self.assertIn('not older than the session version', at_version)
        newer_version = '\n'.join(nrlog.version_block('10.40.1', [], self.DATA,
                                                       worked_on='10.53.5'))
        self.assertIn('not older than the session version', newer_version)

    def test_unparseable_worked_on_names_the_value_and_does_not_raise(self):
        text = '\n'.join(nrlog.version_block('10.40.1', [], self.DATA, worked_on='banana'))
        self.assertIn('WORKED-ON', text)
        self.assertIn('banana', text)
        self.assertIn('not a parseable version', text)

    def test_extract_log_terms_drops_the_stoplist_and_short_tokens_keeps_a_library_name(self):
        evidence = [('newrelic_agent.log', 193,
                     'No transaction, skipping method '
                     'Confluent.Kafka.Consumer`2.Consume(System.TimeSpan)')]
        terms = nrlog.extract_log_terms(evidence)
        lowered = [t.lower() for t in terms]
        self.assertIn('kafka', lowered)
        self.assertIn('confluent', lowered)
        self.assertNotIn('system', lowered)
        self.assertNotIn('method', lowered)
        self.assertTrue(all(len(t) >= nrlog.LOG_TERM_MIN_LENGTH for t in terms))

    def test_log_terms_surface_an_entry_the_playbook_keywords_miss(self):
        # Round 3 regression: the Kafka Consume changelog entry shares no word with
        # playbook 8's keywords, so only a log-derived term ('Kafka') can surface it.
        data = {
            'source_label': 'repo checkout', 'latest': '10.45.0',
            'releases': [
                {'version': '10.45.0', 'date': '2026-03-01',
                 'fixes': ['Resolve issues with Kafka "Consume" instrumentation to ensure '
                           'that automatic instrumentation works in conjunction with '
                           'custom instrumentation (#3257)']},
            ],
        }
        playbook_8_keywords = ['wrapper', 'transaction', 'segment']
        without_terms = '\n'.join(nrlog.version_block('10.44.1', playbook_8_keywords, data))
        self.assertNotIn('#3257', without_terms)

        evidence = [('newrelic_agent.log', 193,
                     'No transaction, skipping method '
                     'Confluent.Kafka.Consumer`2.Consume(System.TimeSpan)')]
        terms = nrlog.extract_log_terms(evidence)
        self.assertIn('Kafka', terms)
        with_terms = '\n'.join(nrlog.version_block('10.44.1', playbook_8_keywords, data,
                                                    log_terms=terms))
        self.assertIn('#3257', with_terms)
        self.assertIn('LOG-TERMS', with_terms)

    def test_log_terms_section_is_labelled_separately_from_the_keyword_section(self):
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf'], self.DATA,
                                             log_terms=['reconnect']))
        self.assertIn("this filter uses only the matched playbooks' keywords", text)
        self.assertIn('LOG-TERMS', text)
        self.assertIn('terms pulled from the log, not a playbook', text)

    def test_the_nearest_upgrade_is_shown_first_when_the_keyword_list_is_capped(self):
        # Round 4 regression: with more keyword hits than the cap, the old newest-first
        # order dropped the cheapest upgrade off the list. Fails under that order because
        # it would show the 10.54.0 fix instead of the 10.53.1 fix.
        text = '\n'.join(nrlog.version_block('10.40.1', ['wcf'], self.DATA, limit=1))
        self.assertIn('Guard null OperationContext in WCF MethodInvokerWrapper', text)
        self.assertNotIn('Release held WCF client transaction', text)
        self.assertIn('nearest-upgrade-first', text)

    def test_log_terms_block_shows_nearest_upgrade_first_when_capped(self):
        newer = [
            {'version': '10.50.0', 'date': '2026-06-01',
             'fixes': ['Add Kafka internal metrics (#3555)']},
            {'version': '10.45.0', 'date': '2026-03-01',
             'fixes': ['Resolve issues with Kafka "Consume" instrumentation (#3257)']},
        ]
        text = '\n'.join(nrlog._log_terms_block(['kafka'], newer, limit=1))
        self.assertIn('#3257', text)
        self.assertNotIn('#3555', text)
        self.assertIn('nearest-upgrade-first', text)

    def test_worked_on_window_orders_nearest_upgrade_first(self):
        # Round 4 regression: fails under the old order because 10.53.5's reconnect fix
        # would appear before 10.53.1's WCF fix, even though 10.53.1 is the nearer upgrade.
        text = '\n'.join(nrlog.version_block('10.53.5', ['wcf'], self.DATA,
                                             worked_on='10.40.1'))
        guard_idx = text.index('Guard null OperationContext in WCF MethodInvokerWrapper')
        reconnect_idx = text.index('Send loaded modules on agent reconnect')
        self.assertLess(guard_idx, reconnect_idx)

    def test_unfiltered_list_stays_newest_first(self):
        text = '\n'.join(nrlog.version_block('10.40.1', [], self.DATA))
        newest_idx = text.index('#3772')
        older_idx = text.index('#3534')
        self.assertLess(newest_idx, older_idx)

    def test_extract_log_terms_drops_level_tokens_and_newrelic(self):
        evidence = [('newrelic_agent.log', 1,
                     'NewRelic TRACE VERBOSE Kafka Confluent something')]
        terms = nrlog.extract_log_terms(evidence)
        lowered = [t.lower() for t in terms]
        self.assertNotIn('newrelic', lowered)
        self.assertNotIn('trace', lowered)
        self.assertNotIn('verbose', lowered)
        self.assertIn('kafka', lowered)
        self.assertIn('confluent', lowered)

    def test_full_shape_evidence_line_keeps_kafka_inside_the_cap(self):
        evidence = [('newrelic_agent.log', 42,
                     '2026-09-10 14:22:01,113 NewRelic FINEST 42 No transaction, skipping '
                     'method Confluent.Kafka.Consumer`2.Consume(System.TimeSpan)')]
        terms = nrlog.extract_log_terms(evidence)
        lowered = [t.lower() for t in terms]
        self.assertIn('kafka', lowered)
        self.assertLessEqual(len(terms), nrlog.LOG_TERM_CAP)

    def test_stoplist_covers_every_level_token(self):
        for token in list(nrlog.LEVEL_ORDER.keys()) + list(nrlog.LEVEL_ALIASES.keys()):
            self.assertIn(token.lower(), nrlog.LOG_TERM_STOPLIST)

    def test_triage_prints_the_version_block_by_default(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        nrlog.reset_profiler_cache()
        text = '\n'.join(nrlog.triage_report(path=FIXTURES, file='Quiet', session=None,
                                             playbooks=None, max_matched=5, evidence=2,
                                             width=200, no_version=False))
        self.assertIn('VERSION', text)
        self.assertIn('10.40.1', text)


class ChangelogParsingTests(unittest.TestCase):
    """parse_changelog and clean, moved here from the deleted make_versions.py."""

    def test_clean_strips_markdown_links_and_keeps_one_issue_number(self):
        self.assertEqual(
            nrlog.clean('Fix [thing](http://x) ([#3772](http://y))'),
            'Fix thing (#3772)')

    def test_clean_does_not_duplicate_a_reference_already_in_the_text(self):
        """Part B / R35: the hand-written era links its issue inline, so TRAILER
        matches nothing and the old code duplicated the reference. #1420 must
        appear once."""
        self.assertEqual(
            nrlog.clean('...for more details. #1420 (#1420)'),
            'for more details. #1420')

    def test_clean_keeps_the_hand_written_era_shape(self):
        self.assertEqual(
            nrlog.clean('Fixes [#1459](url), a thing. [#1480](url)'),
            'Fixes #1459, a thing. #1480')

    def test_clean_appends_the_reference_when_not_already_present(self):
        self.assertEqual(
            nrlog.clean('Fix agent connection error when debug logging is enabled (#3395)'),
            'Fix agent connection error when debug logging is enabled (#3395)')

    def test_parse_changelog_reads_both_heading_shapes(self):
        text = ('## [10.54.0](https://x/compare/v10.53.5...v10.54.0) (2026-08-25)\n\n'
                '### Fixes\n\n'
                '* A recent fix ([#3772](url)) ([abc123](url))\n\n'
                '## [10.9.0] - 2023-03-28\n\n'
                '### Fixes\n\n'
                '* An older fix ([#1459](url))\n')
        releases = nrlog.parse_changelog(text)
        self.assertEqual([r['version'] for r in releases], ['10.54.0', '10.9.0'])
        self.assertEqual(releases[0]['date'], '2026-08-25')
        self.assertEqual(releases[0]['fixes'], ['A recent fix (#3772)'])
        self.assertEqual(releases[1]['date'], '2023-03-28')
        self.assertEqual(releases[1]['fixes'], ['An older fix (#1459)'])

    def test_parse_changelog_keeps_only_the_kept_sections(self):
        text = ('## [1.0.0] - 2026-01-01\n\n'
                '### Fixes\n\n'
                '* A fix (#1)\n\n'
                '### Chores\n\n'
                '* Not kept\n')
        releases = nrlog.parse_changelog(text)
        self.assertEqual(releases[0]['fixes'], ['A fix (#1)'])


class ChangelogCorpusTests(unittest.TestCase):
    """R35: this is the test that would have caught Important 1 across all 117
    affected entries rather than the 3 releases a human sampled."""

    def setUp(self):
        if not os.path.isfile(CHANGELOG):
            self.skipTest('src/Agent/CHANGELOG.md not reachable from this checkout')

    def test_no_fix_title_carries_a_duplicated_issue_reference(self):
        with open(CHANGELOG, 'r', encoding='utf-8') as handle:
            text = handle.read()
        releases = nrlog.parse_changelog(text)
        self.assertTrue(releases, 'the real changelog must parse to at least one release')
        offenders = []
        for release in releases:
            for fix in release['fixes']:
                numbers = re.findall(r'#(\d+)', fix)
                if len(numbers) != len(set(numbers)):
                    offenders.append('%s: %s' % (release['version'], fix))
        self.assertEqual(offenders, [], 'duplicated issue reference(s) found')


class ChangelogResolutionTests(unittest.TestCase):
    """One test per row of the Part A resolution-order table. R34: every fetcher
    here is a fake, and every path is explicit; none of these touch the network."""

    def setUp(self):
        self.scratch = tempfile.mkdtemp()
        self.now = datetime(2026, 9, 14, 12, 0, 0, tzinfo=timezone.utc).timestamp()

    def tearDown(self):
        shutil.rmtree(self.scratch, ignore_errors=True)

    def no_checkout(self):
        return mock.patch.object(nrlog, 'local_changelog_path', lambda: None)

    def cache_at(self, path):
        return mock.patch.object(nrlog, 'changelog_cache_path', lambda: path)

    def test_order_1_an_explicit_override_wins_over_everything_else(self):
        override = os.path.join(self.scratch, 'override.md')
        with open(override, 'w', encoding='utf-8') as handle:
            handle.write('## [9.9.9] - 2026-01-01\n')

        def raising_fetcher():
            raise AssertionError('override must win before any fetch is attempted')

        with self.no_checkout():
            text, source = nrlog.resolve_changelog(override=override, fetcher=raising_fetcher,
                                                    now=self.now)
        self.assertIn('9.9.9', text)
        self.assertEqual(source, override)

    def test_order_1_an_unreadable_override_is_a_hard_error_not_a_fall_through(self):
        missing = os.path.join(self.scratch, 'does-not-exist.md')

        def raising_fetcher():
            raise AssertionError('a named override that fails to read must not fall through')

        with self.assertRaises(SystemExit) as ctx:
            nrlog.resolve_changelog(override=missing, fetcher=raising_fetcher, now=self.now)
        self.assertIn(missing, str(ctx.exception))

    def test_order_2_the_repo_checkout_is_used_when_no_override_is_given(self):
        if not os.path.isfile(CHANGELOG):
            self.skipTest('src/Agent/CHANGELOG.md not reachable from this checkout')

        def raising_fetcher():
            raise AssertionError('the checkout copy must win before any fetch is attempted')

        text, source = nrlog.resolve_changelog(fetcher=raising_fetcher, now=self.now)
        self.assertEqual(source, 'repo checkout')
        self.assertTrue(text)

    def test_order_3_a_fresh_cache_is_used_without_fetching(self):
        cache = os.path.join(self.scratch, 'changelog.md')
        with open(cache, 'w', encoding='utf-8') as handle:
            handle.write('## [1.2.3] - 2026-09-01\n')
        os.utime(cache, (self.now - 3600, self.now - 3600))

        def raising_fetcher():
            raise AssertionError('a fresh cache must be used without a fetch')

        with self.no_checkout(), self.cache_at(cache):
            text, source = nrlog.resolve_changelog(fetcher=raising_fetcher, now=self.now)
        self.assertIn('1.2.3', text)
        self.assertTrue(source.startswith('cache from'), source)

    def test_order_4_a_network_fetch_is_used_and_written_to_the_cache(self):
        cache = os.path.join(self.scratch, 'changelog.md')
        fetched = '## [4.5.6] - 2026-09-13\n'

        with self.no_checkout(), self.cache_at(cache):
            text, source = nrlog.resolve_changelog(fetcher=lambda: fetched, now=self.now)
        self.assertEqual(text, fetched)
        self.assertTrue(source.startswith('github, fetched'), source)
        with open(cache, 'r', encoding='utf-8') as handle:
            self.assertEqual(handle.read(), fetched, 'the fetch must be written to the cache')
        self.assertEqual(os.listdir(self.scratch), ['changelog.md'],
                         'the temp file used for the atomic write must not be left behind')

    def test_order_4_a_failed_cache_replace_cleans_up_its_temp_file(self):
        cache_dir = os.path.join(self.scratch, 'cachedir')
        cache = os.path.join(cache_dir, 'changelog.md')
        os.makedirs(cache)
        fetched = '## [6.6.6] - 2026-09-11\n'

        with self.no_checkout(), self.cache_at(cache):
            text, source = nrlog.resolve_changelog(fetcher=lambda: fetched, now=self.now)
        self.assertEqual(text, fetched)
        self.assertTrue(source.startswith('github, fetched'), source)
        self.assertEqual(os.listdir(cache_dir), ['changelog.md'],
                         'a failed os.replace must not leave its temp file behind')

    def test_order_5_a_stale_cache_is_used_when_the_fetch_fails_and_states_its_age(self):
        cache = os.path.join(self.scratch, 'changelog.md')
        with open(cache, 'w', encoding='utf-8') as handle:
            handle.write('## [1.0.0] - 2026-08-01\n')
        just_over_two_days = 2 * 24 * 60 * 60 + 3600
        os.utime(cache, (self.now - just_over_two_days, self.now - just_over_two_days))

        with self.no_checkout(), self.cache_at(cache):
            text, source = nrlog.resolve_changelog(fetcher=lambda: None, now=self.now)
        self.assertIn('1.0.0', text)
        self.assertIn('2 day(s) old', source)

    def test_order_6_nothing_resolves_to_none(self):
        cache = os.path.join(self.scratch, 'changelog.md')
        with self.no_checkout(), self.cache_at(cache):
            text, source = nrlog.resolve_changelog(fetcher=lambda: None, now=self.now)
        self.assertIsNone(text)
        self.assertIsNone(source)
        with self.no_checkout(), self.cache_at(cache):
            self.assertIsNone(nrlog.load_versions(fetcher=lambda: None, now=self.now))

    def test_order_6_the_degraded_report_still_renders_every_other_block(self):
        """The nothing row: the VERSION block states the changelog is unreachable, and
        every other report block renders as usual. R34: fetch_changelog itself is faked
        here so a full triage_report call cannot reach the network either."""
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        cache = os.path.join(self.scratch, 'changelog.md')
        with self.no_checkout(), self.cache_at(cache), \
             mock.patch.object(nrlog, 'fetch_changelog', lambda *a, **k: None):
            nrlog.reset_profiler_cache()
            lines = nrlog.triage_report(path=FIXTURES, file='Quiet', session=None,
                                        playbooks=None, max_matched=5, evidence=2,
                                        width=200, no_version=False)
        text = '\n'.join(lines)
        self.assertIn('changelog not reachable; latest version unknown, '
                      'currency not checked', text)
        for label in ('INPUT', 'SESSION', 'PROFILER', 'MATCHED', 'BLOCKED', 'CLEAR',
                      'ERRORS', 'NEXT'):
            self.assertIn(label, text)


class CliCleanlinessTests(unittest.TestCase):
    """The report goes to stdout; a warning would go to stderr and a support engineer
    would see it above their report. No earlier test would have caught that, since
    every TriageTests case calls triage_report() in-process rather than as a CLI run."""

    def test_a_triage_run_emits_nothing_on_stderr(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
        result = subprocess.run(
            [sys.executable, '-W', 'error::DeprecationWarning', script,
             'triage', FIXTURES, '--file', 'Quiet'],
            capture_output=True)
        stderr = result.stderr.decode('utf-8', errors='replace')
        self.assertEqual(stderr, '', 'unexpected stderr output: %r' % stderr)
        self.assertEqual(result.returncode, 0)


class CliChangelogFlagTests(unittest.TestCase):
    """ChangelogResolutionTests calls resolve_changelog() in-process and cannot prove the
    --changelog flag reaches it from argparse. This test runs the real CLI subprocess."""

    def test_changelog_flag_names_its_path_in_the_version_block(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp()
        try:
            changelog = os.path.join(scratch, 'CHANGELOG.md')
            with open(changelog, 'w', encoding='utf-8') as handle:
                handle.write('## [8.8.8] - 2026-01-02\n')
            script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
            result = subprocess.run(
                [sys.executable, script, 'triage', FIXTURES, '--file', 'Quiet',
                 '--changelog', changelog],
                capture_output=True)
            stdout = result.stdout.decode('utf-8', errors='replace')
            stderr = result.stderr.decode('utf-8', errors='replace')
            self.assertEqual(result.returncode, 0, stderr)
            version_line = next(line for line in stdout.splitlines()
                                if line.startswith('VERSION'))
            self.assertIn(changelog, version_line, version_line)
        finally:
            shutil.rmtree(scratch, ignore_errors=True)


class CliChangelogOverrideErrorTests(unittest.TestCase):
    """An explicit --changelog that cannot be read must fail loudly. This is a separate
    class from CliCleanlinessTests on purpose: that class only ever runs a triage with no
    --changelog flag, so its stderr-must-be-empty assertion still covers only the success
    path, and this class's failure-path assertions never touch that one."""

    def test_a_missing_changelog_path_exits_nonzero_and_names_itself_on_stderr(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp()
        try:
            missing = os.path.join(scratch, 'does-not-exist.md')
            script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
            result = subprocess.run(
                [sys.executable, script, 'triage', FIXTURES, '--file', 'Quiet',
                 '--changelog', missing],
                capture_output=True)
            stderr = result.stderr.decode('utf-8', errors='replace')
            self.assertNotEqual(result.returncode, 0)
            self.assertIn(missing, stderr)
        finally:
            shutil.rmtree(scratch, ignore_errors=True)


class CliWorkedOnFlagTests(unittest.TestCase):
    """version_block's own tests call it in-process and cannot prove --worked-on reaches
    it from argparse. This test runs the real CLI subprocess, the same shape as
    CliChangelogFlagTests, so the add_argument -> cmd_triage -> triage_report seam is
    covered end to end, not only the function."""

    def test_worked_on_flag_reaches_the_version_block(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp()
        try:
            changelog = os.path.join(scratch, 'CHANGELOG.md')
            with open(changelog, 'w', encoding='utf-8') as handle:
                handle.write('## [10.40.1] - 2026-01-05\n\n### Fixes\n\n'
                             '* A later fix (#1)\n')
            script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
            result = subprocess.run(
                [sys.executable, script, 'triage', FIXTURES, '--file', 'Quiet',
                 '--changelog', changelog, '--worked-on', '10.40.0'],
                capture_output=True)
            stdout = result.stdout.decode('utf-8', errors='replace')
            stderr = result.stderr.decode('utf-8', errors='replace')
            self.assertEqual(result.returncode, 0, stderr)
            self.assertIn('WORKED-ON', stdout)
            self.assertIn('A later fix', stdout)
        finally:
            shutil.rmtree(scratch, ignore_errors=True)


class SummaryTests(unittest.TestCase):

    def setUp(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        self.books = {b.id: b for b in nrlog.load_playbooks()}

    def escalate(self, **overrides):
        target = tempfile.mkdtemp(prefix='nrlog-test-')
        self.addCleanup(shutil.rmtree, target, True)
        kwargs = dict(path=FIXTURES, file='Quiet', session=None, ticket='TEST-1',
                      out=target, make_zip=False, playbooks=None)
        kwargs.update(overrides)
        return nrlog.build_escalation(**kwargs)

    def test_customer_block_has_fix_and_next_ask_and_no_log_line(self):
        lines = nrlog.customer_block(self.books[2])
        text = '\n'.join(lines)
        self.assertIn('NEW_RELIC_INCLUDED_APPLICATION_NAMES', text)
        self.assertIn('Next ask', text)
        self.assertNotRegex(text, r'\[pid: \d+, tid: \d+\]')

    def test_field_tier_block_carries_the_unverified_header(self):
        field_dir = os.path.join(PLAYBOOK_FIXTURES, 'field')
        if not os.path.isdir(field_dir):
            self.skipTest('run python tests/make_playbook_fixtures.py first')
        book = nrlog.load_playbooks(field_dir)[0]
        text = '\n'.join(nrlog.customer_block(book))
        self.assertIn('field-observed', text)
        self.assertIn('not source-verified', text)

    def test_verified_block_has_no_unverified_header(self):
        text = '\n'.join(nrlog.customer_block(self.books[2]))
        self.assertNotIn('field-observed', text)

    def test_escalation_packet_writes_every_file_and_warns(self):
        written, warnings = self.escalate()
        names = sorted(os.path.basename(p) for p in written)
        self.assertIn('triage-report.txt', names)
        self.assertIn('environment.txt', names)
        self.assertTrue(any(n.endswith('-slim.log') for n in names), names)
        self.assertTrue(any('host name' in w for w in warnings))

    def test_escalation_slim_file_is_named_by_session_number(self):
        written, _warnings = self.escalate()
        slim = [p for p in written if p.endswith('-slim.log')]
        self.assertEqual([os.path.basename(p) for p in slim], ['session-1-slim.log'])

    def test_escalation_slim_file_is_redacted(self):
        written, _warnings = self.escalate(ticket='TEST-2')
        checked = 0
        for path in written:
            if path.endswith('-slim.log'):
                with open(path, 'r', encoding='utf-8') as handle:
                    body = handle.read()
                self.assertNotRegex(body, r'[0-9a-zA-Z]{36}NRAL')
                self.assertIn('[REDACTED]', body)
                checked += 1
        self.assertEqual(checked, 1, 'the packet must carry exactly one slim log')

    def test_escalation_slim_file_drops_levels_below_info(self):
        written, _warnings = self.escalate(file='ConfigDebug')
        slim = next(p for p in written if p.endswith('-slim.log'))
        with open(slim, 'r', encoding='utf-8') as handle:
            entries = [l for l in handle.read().splitlines() if not l.startswith('#')]
        self.assertTrue(entries)
        levels = {nrlog.MANAGED_RE.match(l).group(2).rstrip(':') for l in entries}
        self.assertTrue(levels <= set(nrlog.ESCALATION_LEVELS), levels)
        self.assertNotIn('DEBUG', levels)
        self.assertIn('INFO', levels)

    def test_escalation_profiler_copy_is_named_by_pid(self):
        written, _warnings = self.escalate(file='MyApp.log', session=1)
        names = [os.path.basename(p) for p in written]
        self.assertIn('profiler-8412.log', names, names)

    def test_profiler_name_uses_the_pid_from_the_source_name(self):
        self.assertEqual(
            nrlog.escalation_profiler_name(os.path.join('logs', 'NewRelic.Profiler.8412.log')),
            'profiler-8412.log')

    def test_profiler_name_keeps_a_basename_that_carries_no_pid(self):
        """profiler_logs_for_session only returns names the pid regex matched, so this
        fallback has no path through build_escalation and is tested directly."""
        self.assertEqual(
            nrlog.escalation_profiler_name(os.path.join('logs', 'renamed-by-customer.log')),
            'renamed-by-customer.log')

    def test_environment_file_states_the_runtime_it_parsed(self):
        written, _warnings = self.escalate(file='ConfigDebug')
        environment = next(p for p in written if p.endswith('environment.txt'))
        with open(environment, 'r', encoding='utf-8') as handle:
            body = handle.read()
        self.assertIn('runtime: .NET 8.0.10', body)
        self.assertIn('os: not recorded in an agent log line', body)

    def test_environment_file_names_the_limit_when_no_runtime_line_exists(self):
        written, _warnings = self.escalate()
        environment = next(p for p in written if p.endswith('environment.txt'))
        with open(environment, 'r', encoding='utf-8') as handle:
            body = handle.read()
        self.assertIn('runtime: not stated (the agent logs it at DEBUG)', body)
        self.assertIn('os: not recorded in an agent log line', body)
        self.assertIn('DEBUG and FINEST are dropped', body)

    def test_session_runtime_is_parsed_where_the_agent_logged_it(self):
        self.assertEqual(load_session('ConfigDebug').runtime, '.NET 8.0.10')

    def test_session_runtime_is_none_where_the_agent_never_logged_it(self):
        self.assertIsNone(load_session('Quiet').runtime)

    def test_escalation_zip_carries_every_written_file(self):
        import zipfile
        written, _warnings = self.escalate(make_zip=True)
        archive = next(p for p in written if p.endswith('.zip'))
        with zipfile.ZipFile(archive) as bundle:
            names = sorted(os.path.basename(n) for n in bundle.namelist())
        self.assertIn('triage-report.txt', names)
        self.assertIn('environment.txt', names)
        self.assertIn('session-1-slim.log', names)


class CliSummaryTests(unittest.TestCase):
    """The summary command as a support engineer runs it: stdout carries the block, and
    stderr stays empty on a normal run."""

    def run_cli(self, *arguments):
        script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
        result = subprocess.run([sys.executable, script] + list(arguments),
                                capture_output=True)
        return (result.returncode, result.stdout.decode('utf-8', errors='replace'),
                result.stderr.decode('utf-8', errors='replace'))

    def test_customer_block_prints_on_stdout_with_a_clean_stderr(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        code, stdout, stderr = self.run_cli('summary', FIXTURES, '--playbook', '2')
        self.assertEqual(code, 0, stderr)
        self.assertEqual(stderr, '', 'unexpected stderr output: %r' % stderr)
        self.assertIn('NEW_RELIC_INCLUDED_APPLICATION_NAMES', stdout)

    def test_an_unknown_playbook_id_fails_loudly(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        code, _stdout, stderr = self.run_cli('summary', FIXTURES, '--playbook', '9999')
        self.assertNotEqual(code, 0)
        self.assertIn('no playbook with id 9999', stderr)

    def test_neither_playbook_nor_escalation_fails_loudly(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        code, _stdout, stderr = self.run_cli('summary', FIXTURES)
        self.assertNotEqual(code, 0)
        self.assertIn('--playbook', stderr)

    def test_escalation_with_an_unreadable_changelog_override_fails_loudly(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp(prefix='nrlog-test-')
        self.addCleanup(shutil.rmtree, scratch, True)
        missing = os.path.join(scratch, 'does-not-exist.md')
        code, _stdout, stderr = self.run_cli(
            'summary', FIXTURES, '--file', 'Quiet', '--escalation',
            '--out', os.path.join(scratch, 'work'), '--changelog', missing)
        self.assertNotEqual(code, 0)
        self.assertIn(missing, stderr)

    def test_escalation_names_its_changelog_override_in_the_embedded_report(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp(prefix='nrlog-test-')
        self.addCleanup(shutil.rmtree, scratch, True)
        changelog = os.path.join(scratch, 'CHANGELOG.md')
        with open(changelog, 'w', encoding='utf-8') as handle:
            handle.write('## [8.8.8] - 2026-01-02\n')
        code, stdout, stderr = self.run_cli(
            'summary', FIXTURES, '--file', 'Quiet', '--escalation', '--ticket', 'CL-1',
            '--out', os.path.join(scratch, 'work'), '--changelog', changelog)
        self.assertEqual(code, 0, stderr)
        self.assertEqual(stderr, '', 'unexpected stderr output: %r' % stderr)
        report = os.path.join(scratch, 'work', 'escalation-CL-1', 'triage-report.txt')
        with open(report, 'r', encoding='utf-8') as handle:
            body = handle.read()
        version_line = next(line for line in body.splitlines()
                            if line.startswith('VERSION'))
        self.assertIn(changelog, version_line, version_line)
        self.assertIn('wrote ', stdout)


class DraftTests(unittest.TestCase):

    def setUp(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')

    def test_normalize_shape_strips_variable_parts(self):
        shape = nrlog.normalize_shape('Transaction 41 for "MyApp" took 1234 ms')
        self.assertNotIn('41', shape)
        self.assertNotIn('MyApp', shape)
        self.assertIn('Transaction', shape)

    def test_normalize_shape_strips_a_hex_address(self):
        shape = nrlog.normalize_shape('Handle 0xDEADBEEF released')
        self.assertNotIn('0xDEADBEEF', shape)
        self.assertIn('0xN', shape)

    def test_normalize_shape_collapses_runs_of_whitespace(self):
        self.assertEqual(nrlog.normalize_shape('a   b\tc'), 'a b c')

    def test_normalize_shape_caps_its_length(self):
        self.assertEqual(len(nrlog.normalize_shape('x' * 400)), 160)

    def test_candidates_exclude_lines_a_playbook_already_matches(self):
        session = load_session('Quiet')
        books = nrlog.load_playbooks()
        candidates = nrlog.candidate_signatures(session, books)
        for shape, _count in candidates:
            for book in books:
                for signature in book.signatures:
                    self.assertNotIn(signature, shape)

    def test_candidates_drop_the_line_that_carries_a_known_signature(self):
        """The Quiet 401 line is playbook 3's, and it is the one ERROR in the session."""
        session = load_session('Quiet')
        candidates = nrlog.candidate_signatures(session, nrlog.load_playbooks())
        self.assertTrue(candidates)
        for shape, _count in candidates:
            self.assertNotIn('401', shape)
            self.assertNotIn('Unauthorized', shape)

    def test_candidates_drop_levels_more_verbose_than_info(self):
        session = load_session('MyApp.log')
        candidates = nrlog.candidate_signatures(session, [], limit=50)
        shapes = [shape for shape, _count in candidates]
        self.assertTrue(shapes)
        self.assertFalse([s for s in shapes if 'Environment Variable' in s], shapes)
        self.assertFalse([s for s in shapes if 'skipping method' in s], shapes)

    def test_candidates_are_ranked_by_count_then_shape(self):
        session = load_session('Shared')
        candidates = nrlog.candidate_signatures(session, [], limit=50)
        counts = [count for _shape, count in candidates]
        self.assertEqual(counts, sorted(counts, reverse=True), candidates)
        self.assertIn(('Log level set to INFO', 3), candidates, candidates)

    def test_candidates_drop_a_shape_too_short_to_be_a_signature(self):
        """No shipped fixture carries a message under SHAPE_MIN characters, so this
        writes its own one-session log rather than lengthen a shared fixture."""
        scratch = tempfile.mkdtemp(prefix='nrlog-test-')
        self.addCleanup(shutil.rmtree, scratch, True)
        path = os.path.join(scratch, 'newrelic_agent_Short.log')
        with open(path, 'w', encoding='utf-8') as handle:
            handle.write('2026-08-22 10:00:00,000 NewRelic   INFO: [pid: 4242, tid: 1] '
                         "The New Relic .NET Agent v10.44.0 started (pid 4242) on app "
                         "domain '/Short'\n")
            handle.write('2026-08-22 10:00:01,000 NewRelic   INFO: [pid: 4242, tid: 1] '
                         'Ready\n')
            handle.write('2026-08-22 10:00:02,000 NewRelic   INFO: [pid: 4242, tid: 1] '
                         "The New Relic .NET Agent v10.44.0 has shutdown (pid 4242) on app "
                         "domain '/Short'\n")
        session = nrlog.build_sessions([path])[0]
        shapes = [shape for shape, _count in
                  nrlog.candidate_signatures(session, [], limit=50)]
        self.assertTrue(shapes)
        self.assertNotIn('Ready', shapes)

    def test_candidates_ignore_a_later_session_of_the_same_pid(self):
        session = load_session('MyApp.log')
        candidates = nrlog.candidate_signatures(session, [], limit=50)
        shapes = [shape for shape, _count in candidates]
        self.assertTrue([s for s in shapes if 'collector-inside' in s], shapes)
        self.assertFalse([s for s in shapes if 'collector-later' in s], shapes)

    def test_candidates_count_only_the_lines_this_session_owns(self):
        session = load_session('MyApp.log')
        candidates = nrlog.candidate_signatures(session, [], limit=50)
        expected = sum(count for token, count in session.level_counts.items()
                       if nrlog.LEVEL_ORDER[nrlog.normalize_level(token)]
                       <= nrlog.LEVEL_ORDER['INFO'])
        self.assertEqual(sum(count for _shape, count in candidates), expected)

    def test_candidates_honour_the_limit(self):
        session = load_session('MyApp.log')
        self.assertEqual(len(nrlog.candidate_signatures(session, [], limit=2)), 2)

    def test_draft_text_validates_as_a_playbook(self):
        session = load_session('Quiet')
        candidates = nrlog.candidate_signatures(session, nrlog.load_playbooks())
        text = nrlog.draft_playbook_text(session, candidates, next_id=500)
        fields, body = nrlog.parse_frontmatter(text)
        self.assertEqual(fields['tier'], 'field')
        self.assertEqual(fields['observed_in'], session.version)
        self.assertIn('## Customer fix', body)
        self.assertIn('## Next ask', body)
        errors = nrlog.validate_playbook(fields, 'draft.md')
        self.assertEqual(errors, [], errors)

    def test_draft_text_states_the_observed_level_as_min_level(self):
        session = load_session('Quiet')
        fields, _body = nrlog.parse_frontmatter(
            nrlog.draft_playbook_text(session, [], next_id=500))
        self.assertEqual(fields['min_level'], 'info')

    def test_draft_text_with_no_candidates_still_validates(self):
        session = load_session('Quiet')
        fields, _body = nrlog.parse_frontmatter(
            nrlog.draft_playbook_text(session, [], next_id=500))
        self.assertEqual(fields['signatures'], ['REPLACE ME'])
        self.assertEqual(nrlog.validate_playbook(fields, 'draft.md'), [])

    def test_draft_text_quotes_a_shape_that_carries_a_double_quote(self):
        session = load_session('Quiet')
        text = nrlog.draft_playbook_text(session, [('he said "no" loudly', 2)], next_id=500)
        fields, _body = nrlog.parse_frontmatter(text)
        self.assertEqual(fields['signatures'], ["he said 'no' loudly"])

    def test_draft_text_carries_the_candidate_counts_as_a_comment(self):
        session = load_session('Quiet')
        text = nrlog.draft_playbook_text(session, [('some unmatched shape', 7)], next_id=500)
        self.assertIn('x7', text)
        self.assertIn('some unmatched shape', text)


class CliDraftTests(unittest.TestCase):

    def test_the_written_draft_round_trips_through_the_validator(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp(prefix='nrlog-test-')
        self.addCleanup(shutil.rmtree, scratch, True)
        script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
        result = subprocess.run(
            [sys.executable, script, 'draft-playbook', FIXTURES, '--file', 'Quiet',
             '--out', scratch], capture_output=True)
        stdout = result.stdout.decode('utf-8', errors='replace')
        stderr = result.stderr.decode('utf-8', errors='replace')
        self.assertEqual(result.returncode, 0, stderr)
        self.assertEqual(stderr, '', 'unexpected stderr output: %r' % stderr)
        target = os.path.join(scratch, '100-draft-playbook.md')
        self.assertIn(target, stdout)
        with open(target, 'r', encoding='utf-8') as handle:
            fields, _body = nrlog.parse_frontmatter(handle.read())
        self.assertEqual(nrlog.validate_playbook(fields, target), [])

    def test_an_explicit_id_names_the_file(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        scratch = tempfile.mkdtemp(prefix='nrlog-test-')
        self.addCleanup(shutil.rmtree, scratch, True)
        script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
        result = subprocess.run(
            [sys.executable, script, 'draft-playbook', FIXTURES, '--file', 'Quiet',
             '--out', scratch, '--id', '777'], capture_output=True)
        self.assertEqual(result.returncode, 0,
                         result.stderr.decode('utf-8', errors='replace'))
        self.assertTrue(os.path.isfile(os.path.join(scratch, '777-draft-playbook.md')))


class CommandSurfaceTests(unittest.TestCase):

    def test_every_expected_command_runs_with_help(self):
        script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
        for command in ('sessions', 'slim', 'extract', 'triage', 'summary',
                        'draft-playbook', 'payloads', 'body', 'decode', 'profiler',
                        'instrumented'):
            result = subprocess.run([sys.executable, script, command, '--help'],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0,
                             '%s --help failed: %s' % (command, result.stderr))

    def test_slim_and_its_extract_alias_write_the_same_file(self):
        if not os.path.isdir(FIXTURES):
            self.skipTest('run python tests/make_fixtures.py first')
        script = os.path.join(HERE, '..', 'scripts', 'nrlog.py')
        written = {}
        for command in ('slim', 'extract'):
            scratch = tempfile.mkdtemp(prefix='nrlog-test-')
            self.addCleanup(shutil.rmtree, scratch, True)
            result = subprocess.run(
                [sys.executable, script, command, FIXTURES, '--file', 'Quiet',
                 '--out', scratch], capture_output=True)
            stderr = result.stderr.decode('utf-8', errors='replace')
            self.assertEqual(result.returncode, 0, stderr)
            self.assertEqual(stderr, '', 'unexpected stderr output: %r' % stderr)
            produced = os.listdir(scratch)
            self.assertEqual(len(produced), 1, produced)
            with open(os.path.join(scratch, produced[0]), 'r', encoding='utf-8') as handle:
                written[command] = (produced[0], handle.read())
        self.assertEqual(written['slim'], written['extract'])

    def test_the_skill_table_carries_one_row_per_command(self):
        """A row, not a mention: a command named only in the workflow prose would
        otherwise satisfy this and the table could silently lose a command."""
        skill = os.path.join(HERE, '..', 'SKILL.md')
        with open(skill, 'r', encoding='utf-8') as handle:
            rows = [line for line in handle.read().splitlines()
                    if line.startswith('| `')]
        documented = [line.split('`')[1] for line in rows]
        self.assertEqual(
            documented,
            ['sessions', 'triage', 'slim', 'summary', 'draft-playbook', 'payloads',
             'body', 'decode', 'profiler', 'instrumented'],
            documented)

    def test_the_skill_states_the_hidden_extract_alias(self):
        skill = os.path.join(HERE, '..', 'SKILL.md')
        with open(skill, 'r', encoding='utf-8') as handle:
            text = handle.read()
        self.assertIn('`extract` is a hidden alias of `slim`', text)


if __name__ == '__main__':
    unittest.main()
