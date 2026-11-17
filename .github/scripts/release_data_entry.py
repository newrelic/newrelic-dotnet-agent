#!/usr/bin/env python3
"""Add a release's entry to release-data/download-site-hashes.json.

Run by the release-notes workflow (publish_release_notes.yml) after a deploy. It hashes the files the release deployed to the download
site (the DownloadSite folder of the release build's deploy-artifacts), checks each hash against the SHA256/*.sha256 file the build wrote
next to it, adds the unversioned copies that the deploy makes, and writes the entry. The workflow then opens a pull request, so a person
reviews the entry before it is merged.

Standard library only (no pip install). Tests: .github/scripts/test_release_data_entry.py, run by the Workflow Lint workflow.

    python3 .github/scripts/release_data_entry.py --download-site <dir> --version 10.58.0 --run-id 123 \
        --data-file release-data/download-site-hashes.json [--dry-run]

Exit codes: 0 entry added, or already present and identical; 1 an error (nothing is written); 2 usage.
"""
import argparse
import datetime
import hashlib
import json
import os
import re
import sys

HASHABLE = re.compile(r'\.(msi|zip|tar\.gz|deb|rpm)$')
SHA256_RE = re.compile(r'^[0-9a-f]{64}$')


class ReleaseDataError(Exception):
    """Something is wrong with the inputs; the message says what, for the workflow log."""


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def unversioned_name(name, version):
    """The name deploy_agent.yml copies a file to (its 'Copy-Item ... NewRelicDotNetAgent_x64.msi' lines), or None if it makes no copy.

    Only the installers, zips and tar.gz files get one; debs and rpms do not.
    """
    v = re.escape(version)
    m = re.fullmatch(r'NewRelicDotNetAgent_' + v + r'_(x64|x86)\.(msi|zip)', name)
    if m:
        return 'NewRelicDotNetAgent_%s.%s' % (m.group(1), m.group(2))
    m = re.fullmatch(r'newrelic-dotnet-agent_' + v + r'_(amd64|arm64)\.tar\.gz', name)
    if m:
        return 'newrelic-dotnet-agent_%s.tar.gz' % m.group(1)
    return None


def read_sha_file(path):
    """A .sha256 file holds the hash alone, or the hash and the file name (the Linux ones); returns the lower-case hash."""
    with open(path, encoding='utf-8') as f:
        words = f.read().split()
    h = words[0].lower() if words else ''
    if not SHA256_RE.match(h):
        raise ReleaseDataError('%s does not start with a sha256' % path)
    return h


def build_entry(download_site, version, run_id, today):
    """Hash every installer and package in download_site and cross-check each against its SHA256/<name>.sha256 file."""
    if not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ReleaseDataError("version '%s' does not look like 10.0.0" % version)
    if not os.path.isdir(download_site):
        raise ReleaseDataError('%s is not a directory' % download_site)
    names = sorted(n for n in os.listdir(download_site) if HASHABLE.search(n) and os.path.isfile(os.path.join(download_site, n)))
    if not names:
        raise ReleaseDataError('no installers or packages in %s' % download_site)
    files = {}
    for name in names:
        if version not in name:
            raise ReleaseDataError('%s does not contain the version %s; refusing to record it' % (name, version))
        sha_path = os.path.join(download_site, 'SHA256', name + '.sha256')
        if not os.path.isfile(sha_path):
            raise ReleaseDataError('%s has no SHA256/%s.sha256 to check it against' % (name, name))
        actual = sha256_file(os.path.join(download_site, name))
        recorded = read_sha_file(sha_path)
        if actual != recorded:
            raise ReleaseDataError('%s: its hash %s differs from the build\'s own SHA256 file %s' % (name, actual, recorded))
        files[name] = actual
    for name in list(files):
        copy = unversioned_name(name, version)
        if copy:
            files[copy] = files[name]
    return {
        'recorded': today,
        'source': 'release-build run %s: hashed from the files the release deployed' % run_id,
        'cross_checks': ['release-build-checksums'],
        'files': dict(sorted(files.items())),
    }


def version_key(version):
    return [int(p) for p in version.split('.')]


def add_entry(data, version, entry):
    """Returns (new data, changed). Keeps the existing order of versions (oldest first) and puts the new one where it belongs."""
    if data.get('schema') != 1 or not isinstance(data.get('versions'), dict):
        raise ReleaseDataError('the data file is not schema 1')
    if version in data.get('unverified', {}):
        raise ReleaseDataError('%s is listed as unverified; remove that entry deliberately in a reviewed change first' % version)
    existing = data['versions'].get(version)
    if existing is not None:
        if existing['files'] == entry['files']:
            return data, False
        raise ReleaseDataError('%s is already recorded with different hashes; refusing to overwrite it' % version)
    out = {}
    placed = False
    for v, e in data['versions'].items():
        if not placed and version_key(v) > version_key(version):
            out[version] = entry
            placed = True
        out[v] = e
    if not placed:
        out[version] = entry
    new = dict(data)
    new['versions'] = out
    return new, True


def dump(data):
    """The format the file is written in: one space of indent, keys in the order they are in, a final newline."""
    return json.dumps(data, indent=1, ensure_ascii=False) + '\n'


def main(argv):
    p = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    p.add_argument('--download-site', required=True, help='the DownloadSite folder of deploy-artifacts')
    p.add_argument('--version', required=True)
    p.add_argument('--run-id', required=True, help='the release_build run the files came from')
    p.add_argument('--data-file', required=True)
    p.add_argument('--dry-run', action='store_true', help='print the entry and change nothing')
    a = p.parse_args(argv)
    try:
        with open(a.data_file, encoding='utf-8') as f:
            data = json.load(f)
        entry = build_entry(a.download_site, a.version, a.run_id, datetime.date.today().isoformat())
        new, changed = add_entry(data, a.version, entry)
    except (ReleaseDataError, OSError, ValueError, KeyError) as e:
        print('::error::release data: %s' % e)
        return 1
    print('%s: %d files' % (a.version, len(entry['files'])))
    for name, sha in entry['files'].items():
        print('  %s  %s' % (sha, name))
    if not changed:
        print('already recorded with the same hashes; nothing to do')
        return 0
    if a.dry_run:
        print('dry run: %s not written' % a.data_file)
        return 0
    with open(a.data_file, 'w', encoding='utf-8', newline='\n') as f:
        f.write(dump(new))
    print('wrote %s' % a.data_file)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
