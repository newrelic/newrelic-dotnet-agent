"""Tests for release_data_entry.py. Run: python3 -m unittest discover -s .github/scripts -p 'test_*.py'

Standard library only. The Workflow Lint workflow runs these on any pull request that touches .github/**.
"""
import contextlib
import hashlib
import io
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import release_data_entry as r  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
DATA_FILE = os.path.join(REPO, 'release-data', 'download-site-hashes.json')
V = '10.58.0'
NAMES = [
    'NewRelicDotNetAgent_10.58.0_x64.msi', 'NewRelicDotNetAgent_10.58.0_x86.msi',
    'NewRelicDotNetAgent_10.58.0_x64.zip', 'NewRelicDotNetAgent_10.58.0_x86.zip',
    'newrelic-dotnet-agent_10.58.0_amd64.tar.gz', 'newrelic-dotnet-agent_10.58.0_arm64.tar.gz',
    'newrelic-dotnet-agent_10.58.0_amd64.deb', 'newrelic-dotnet-agent_10.58.0_arm64.deb',
    'newrelic-dotnet-agent-10.58.0-1.x86_64.rpm',
]
COPIES = [
    'NewRelicDotNetAgent_x64.msi', 'NewRelicDotNetAgent_x86.msi', 'NewRelicDotNetAgent_x64.zip', 'NewRelicDotNetAgent_x86.zip',
    'newrelic-dotnet-agent_amd64.tar.gz', 'newrelic-dotnet-agent_arm64.tar.gz',
]


def sha(content):
    return hashlib.sha256(content).hexdigest()


def make_download_site(root, names=NAMES, with_sha=True):
    """A DownloadSite folder as the release build leaves it: the files, and SHA256/<name>.sha256 beside them."""
    os.makedirs(os.path.join(root, 'SHA256'), exist_ok=True)
    for n in names:
        content = ('content of ' + n).encode()
        with open(os.path.join(root, n), 'wb') as f:
            f.write(content)
        if with_sha:
            # the Windows files hold the hash alone, the Linux ones the hash and the file name
            text = sha(content).upper() if n.endswith(('.msi', '.zip')) else sha(content) + '  ' + n
            with open(os.path.join(root, 'SHA256', n + '.sha256'), 'w') as f:
                f.write(text + '\n')
    with open(os.path.join(root, 'Readme.txt'), 'w') as f:
        f.write('not hashed')


def data_with(*versions):
    return {'schema': 1, 'versions': {v: {'files': {'x_%s.msi' % v: 'a' * 64}} for v in versions}}


class UnversionedName(unittest.TestCase):
    def test_the_six_copies_deploy_agent_makes(self):
        self.assertEqual([r.unversioned_name(n, V) for n in NAMES if r.unversioned_name(n, V)], COPIES)

    def test_debs_and_rpms_get_no_copy(self):
        for n in ('newrelic-dotnet-agent_10.58.0_amd64.deb', 'newrelic-dotnet-agent-10.58.0-1.x86_64.rpm'):
            self.assertIsNone(r.unversioned_name(n, V))

    def test_a_file_of_another_version_gets_no_copy(self):
        self.assertIsNone(r.unversioned_name('NewRelicDotNetAgent_10.57.0_x64.msi', V))

    def test_the_dots_of_the_version_are_not_wildcards(self):
        self.assertIsNone(r.unversioned_name('NewRelicDotNetAgent_10x58y0_x64.msi', V))


class ReadShaFile(unittest.TestCase):
    def read(self, text):
        with tempfile.TemporaryDirectory() as d:
            p = os.path.join(d, 'x.sha256')
            with open(p, 'w') as f:
                f.write(text)
            return r.read_sha_file(p)

    def test_hash_alone_or_with_the_file_name(self):
        h = 'ab' * 32
        self.assertEqual(self.read(h + '\n'), h)
        self.assertEqual(self.read(h + '  file.deb\n'), h)

    def test_upper_case_is_lowered(self):
        self.assertEqual(self.read(('AB' * 32) + '\n'), 'ab' * 32)

    def test_anything_else_is_an_error(self):
        for bad in ('', 'not a hash', 'ab' * 31, 'zz' * 32):
            with self.assertRaises(r.ReleaseDataError):
                self.read(bad)


class BuildEntry(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = self.tmp.name

    def test_every_package_and_its_copies_are_recorded_with_the_right_hash(self):
        make_download_site(self.root)
        e = r.build_entry(self.root, V, '123', '2026-12-01')
        self.assertEqual(sorted(e['files']), sorted(NAMES + COPIES))
        for n in NAMES:
            self.assertEqual(e['files'][n], sha(('content of ' + n).encode()))
        for n in NAMES:
            c = r.unversioned_name(n, V)
            if c:
                self.assertEqual(e['files'][c], e['files'][n], c)
        self.assertEqual(list(e['files']), sorted(e['files']))
        self.assertEqual((e['recorded'], e['cross_checks']), ('2026-12-01', ['release-build-checksums']))
        self.assertIn('run 123', e['source'])

    def test_readme_and_the_sha256_folder_are_not_recorded(self):
        make_download_site(self.root)
        files = r.build_entry(self.root, V, '1', 'd')['files']
        self.assertFalse([n for n in files if 'Readme' in n or n.endswith('.sha256')])

    def test_a_hash_that_differs_from_the_builds_own_file_is_refused(self):
        make_download_site(self.root)
        with open(os.path.join(self.root, NAMES[0]), 'ab') as f:
            f.write(b'changed after the build wrote its hash')
        with self.assertRaisesRegex(r.ReleaseDataError, 'differs from the build'):
            r.build_entry(self.root, V, '1', 'd')

    def test_a_package_with_no_sha256_file_is_refused(self):
        make_download_site(self.root)
        os.remove(os.path.join(self.root, 'SHA256', NAMES[3] + '.sha256'))
        with self.assertRaisesRegex(r.ReleaseDataError, 'has no SHA256'):
            r.build_entry(self.root, V, '1', 'd')

    def test_a_package_of_another_version_is_refused(self):
        make_download_site(self.root, NAMES[:2] + ['NewRelicDotNetAgent_10.57.0_x64.zip'])
        with self.assertRaisesRegex(r.ReleaseDataError, 'does not contain the version'):
            r.build_entry(self.root, V, '1', 'd')

    def test_an_empty_folder_a_missing_folder_and_a_bad_version_are_refused(self):
        with self.assertRaisesRegex(r.ReleaseDataError, 'no installers'):
            r.build_entry(self.root, V, '1', 'd')
        with self.assertRaisesRegex(r.ReleaseDataError, 'not a directory'):
            r.build_entry(os.path.join(self.root, 'nope'), V, '1', 'd')
        for bad in ('10.58', '10.58.0.1', 'v10.58.0', ''):
            with self.assertRaisesRegex(r.ReleaseDataError, 'does not look like'):
                r.build_entry(self.root, bad, '1', 'd')


class AddEntry(unittest.TestCase):
    ENTRY = {'files': {'x_new.msi': 'b' * 64}}

    def test_a_newer_version_goes_last(self):
        new, changed = r.add_entry(data_with('10.56.0', '10.57.0'), '10.58.0', self.ENTRY)
        self.assertTrue(changed)
        self.assertEqual(list(new['versions']), ['10.56.0', '10.57.0', '10.58.0'])

    def test_versions_sort_by_number_not_by_text(self):
        new, _ = r.add_entry(data_with('10.9.0', '10.20.0'), '10.10.0', self.ENTRY)
        self.assertEqual(list(new['versions']), ['10.9.0', '10.10.0', '10.20.0'])

    def test_existing_entries_are_not_reordered_or_changed(self):
        before = data_with('9.9.0', '10.0.0')
        new, _ = r.add_entry(before, '10.1.0', self.ENTRY)
        self.assertEqual({k: new['versions'][k] for k in before['versions']}, before['versions'])

    def test_the_input_is_not_modified(self):
        before = data_with('10.57.0')
        r.add_entry(before, '10.58.0', self.ENTRY)
        self.assertEqual(list(before['versions']), ['10.57.0'])

    def test_the_same_hashes_again_change_nothing(self):
        d = data_with('10.57.0')
        same = {'files': dict(d['versions']['10.57.0']['files'])}
        new, changed = r.add_entry(d, '10.57.0', same)
        self.assertFalse(changed)
        self.assertEqual(new, d)

    def test_different_hashes_for_a_recorded_version_are_refused(self):
        with self.assertRaisesRegex(r.ReleaseDataError, 'different hashes'):
            r.add_entry(data_with('10.57.0'), '10.57.0', self.ENTRY)

    def test_an_unverified_version_is_refused(self):
        d = data_with('10.57.0')
        d['unverified'] = {'10.20.1': {'reason': 'why', 'files': ['a.msi']}}
        with self.assertRaisesRegex(r.ReleaseDataError, 'unverified'):
            r.add_entry(d, '10.20.1', self.ENTRY)

    def test_another_schema_is_refused(self):
        for bad in ({'schema': 2, 'versions': {}}, {'schema': 1}, {}):
            with self.assertRaises(r.ReleaseDataError):
                r.add_entry(bad, '10.58.0', self.ENTRY)


class RealDataFile(unittest.TestCase):
    """The data file in this repository: the properties the maintenance step relies on."""

    @classmethod
    def setUpClass(cls):
        if not os.path.exists(DATA_FILE):
            raise unittest.SkipTest('no data file at ' + DATA_FILE)
        with open(DATA_FILE, encoding='utf-8', newline='') as f:
            cls.text = f.read().replace('\r\n', '\n')       # a Windows checkout may have CRLF; git stores LF
        cls.data = json.loads(cls.text)

    def test_rewriting_the_file_gives_the_same_bytes(self):
        """So that a release's pull request changes only its own entry."""
        self.assertEqual(r.dump(self.data), self.text)

    def test_every_hash_is_64_lower_case_hex_digits(self):
        for v, e in self.data['versions'].items():
            for n, h in e['files'].items():
                self.assertRegex(h, r'^[0-9a-f]{64}$', v + '/' + n)

    def test_versions_are_oldest_first(self):
        keys = [r.version_key(v) for v in self.data['versions']]
        self.assertEqual(keys, sorted(keys))

    def test_no_version_is_both_recorded_and_unverified(self):
        self.assertFalse(set(self.data['versions']) & set(self.data.get('unverified', {})))

    def test_the_newest_version_has_the_unversioned_copies_the_rule_predicts(self):
        newest = list(self.data['versions'])[-1]
        files = self.data['versions'][newest]['files']
        predicted = {r.unversioned_name(n, newest) for n in files} - {None}
        self.assertEqual(len(predicted), 6)
        for c in predicted:
            self.assertIn(c, files)


class Main(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.site = os.path.join(self.tmp.name, 'DownloadSite')
        make_download_site(self.site)
        self.data = os.path.join(self.tmp.name, 'data.json')
        with open(self.data, 'w', encoding='utf-8', newline='\n') as f:
            f.write(r.dump(data_with('10.57.0')))

    def run_main(self, *extra, version=V):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = r.main(['--download-site', self.site, '--version', version, '--run-id', '7', '--data-file', self.data, *extra])
        return code, out.getvalue()

    def stored(self):
        with open(self.data, encoding='utf-8') as f:
            return json.load(f)

    def test_adds_the_entry_and_the_file_ends_in_one_newline(self):
        code, out = self.run_main()
        self.assertEqual(code, 0)
        self.assertEqual(list(self.stored()['versions']), ['10.57.0', V])
        self.assertEqual(len(self.stored()['versions'][V]['files']), len(NAMES) + len(COPIES))
        with open(self.data, 'rb') as f:
            raw = f.read()
        self.assertTrue(raw.endswith(b'}\n') and not raw.endswith(b'\n\n') and b'\r' not in raw)

    def test_dry_run_prints_and_writes_nothing(self):
        with open(self.data, 'rb') as f:
            before = f.read()
        code, out = self.run_main('--dry-run')
        self.assertEqual(code, 0)
        self.assertIn('dry run', out)
        self.assertIn(NAMES[0], out)
        with open(self.data, 'rb') as f:
            self.assertEqual(f.read(), before)

    def test_running_it_twice_is_harmless(self):
        self.run_main()
        with open(self.data, 'rb') as f:
            once = f.read()
        code, out = self.run_main()
        self.assertEqual(code, 0)
        self.assertIn('already recorded', out)
        with open(self.data, 'rb') as f:
            self.assertEqual(f.read(), once)

    def test_an_error_returns_1_names_the_problem_and_writes_nothing(self):
        with open(os.path.join(self.site, NAMES[0]), 'ab') as f:
            f.write(b'x')
        with open(self.data, 'rb') as f:
            before = f.read()
        code, out = self.run_main()
        self.assertEqual(code, 1)
        self.assertIn('::error::release data:', out)
        with open(self.data, 'rb') as f:
            self.assertEqual(f.read(), before)

    def test_a_missing_data_file_returns_1(self):
        os.remove(self.data)
        code, out = self.run_main()
        self.assertEqual(code, 1)


if __name__ == '__main__':
    unittest.main()
