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
- `unverified` names the files of a version that could not be recorded. The monitoring excuses exactly those file names and
  never hashes them. A version cannot be in both places.

To check a download yourself, compute its SHA-256 (`sha256sum <file>`, `Get-FileHash <file>`) and compare it with the entry for that
file name.

### Adding a release

A release adds an entry for its version in a pull request, with the hashes of the files the release built (the release's
`SHA256/checksums.md` lists them) and the unversioned copies. The monitoring reports a file with no entry as a warning after a
day, so the entry does not need to land before the release, but it should land soon after.

### Removing files from the site

When a version's files are removed from the download site, remove its entry (and any `unverified` entry) in the same pull request.
Otherwise the monitoring reports the files as listed but missing.
