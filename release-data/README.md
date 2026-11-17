# release-data

Facts about what the release process publishes, kept here so that they can be checked against what is actually served. Every change
is a reviewed pull request, which is the point: the files are a record that does not live in the same place as the artifacts.

## download-site-hashes.json

The SHA-256 of every installer and package in `https://download.newrelic.com/dot_net_agent/previous_releases/<version>/`: the
`.msi` and `.zip` files, the `.tar.gz` files, the `.deb` files and the `.rpm`. New Relic's monitoring of the download site reads
this file, hashes the files as a customer would download them, and alerts when one differs.

```json
{
  "schema": 1,
  "versions": {
    "10.57.0": {
      "recorded": "2026-11-16",
      "source": "backfill: hashed from the download site",
      "cross_checks": ["bucket-sha256", "release-notes"],
      "files": { "NewRelicDotNetAgent_10.57.0_x64.msi": "<64 lower-case hex digits>" }
    }
  },
  "unverified": { "<version>": { "reason": "...", "files": ["<file name>"] } }
}
```

- `files` has one entry per file in the version's folder, including the unversioned copies of the same bytes
  (`NewRelicDotNetAgent_x64.msi`, `newrelic-dotnet-agent_amd64.tar.gz`, and so on), which hold the same hash as the versioned file.
- `cross_checks` says which other sources agreed with the site for every file of that version when it was recorded:
  `bucket-sha256` is the `SHA256/` folder next to the files (written by the same release process, so not independent), and
  `release-notes` is the checksum table in the version's release notes. Versions whose release notes carry no table have only the
  first. The initial entries were recorded from the files on the site on 2026-11-16 after these checks.
- `exceptions` (optional) says that one of those sources was set aside for the version, and why. 10.20.1 has one: its release
  notes originally listed hashes that did not match the files on the site, and were corrected.
- `unverified` names the files of a version that could not be recorded. The monitoring excuses exactly those file names and
  never hashes them. A version cannot be in both places.

To check a download yourself, compute its SHA-256 (`sha256sum <file>`, `Get-FileHash <file>`) and compare it with the entry for that
file name.

### Adding a release

The release-notes job of a deploy (`publish_release_notes.yml`) opens a pull request that adds the release's entry, on a branch named
`release-data/<version>`. `.github/scripts/release_data_entry.py` hashes the files the release built and deployed, checks each
against the `SHA256/` file the build wrote for it, adds the unversioned copies the deploy makes, and refuses to overwrite a recorded
version with different hashes. The job runs after the release notes pull request, so a failure here does not hold those up. A person
reviews and merges the data pull request: compare the hashes with the release notes table, or download a file and run
`gh attestation verify <file> --repo newrelic/newrelic-dotnet-agent`.

The monitoring reports a file with no entry as a warning after a day, so the pull request does not need to be merged before the
release is announced, but it should be merged soon after.

If the step fails, or to rehearse it, run the workflow with `dry_run` (nothing is pushed or opened), or add the entry yourself:
`python3 .github/scripts/release_data_entry.py --download-site <the DownloadSite folder of the release build> --version <x.y.z>
--run-id <release_build run> --data-file release-data/download-site-hashes.json`. The script has unit tests
(`python3 -m unittest discover -s .github/scripts -p 'test_*.py'`), which the Workflow Lint workflow runs.

### Removing files from the site

When a version's files are removed from the download site, remove its entry (and any `unverified` entry) in the same pull request.
Otherwise the monitoring reports the files as listed but missing.
