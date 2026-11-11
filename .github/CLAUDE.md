# GitHub workflows

Everything a workflow pulls in must be pinned, so a rewritten tag or a new
upstream release cannot change what CI runs. OpenSSF Scorecard's
`Pinned-Dependencies` check raises a code-scanning alert when it is not.

- **Pin every `uses:` to a full 40-character commit SHA**, with the readable
  version in a trailing comment:
  `uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`.
  A tag or branch ref fails the build. Only two forms are exempt: a local
  `./...` path, and a same-repo reusable workflow
  (`newrelic/newrelic-dotnet-agent/...`).
- **Pin every `pip install` with `--require-hashes -r <file>`.** The hashes
  live in `.github/scripts/pyyaml_requirements.txt`, which Dependabot keeps
  current (`pip` ecosystem, monthly; it rewrites the digests with the version).
  A bare `pip install <pkg>` fails the build. Scorecard accepts no other form
  for a PyPI install.
- Both rules are enforced by `.github/scripts/check-workflows.py`, which
  `.github/workflows/workflow_lint.yml` runs on any PR that touches
  `.github/**`. Run it yourself before you push a workflow change:
  `python3 .github/scripts/check-workflows.py` (it needs PyYAML).
- One `uses:` exception is allow-listed in that script: a shared reusable
  workflow in another New Relic org repo, which we track at `@main` on purpose
  to always get the latest. A same-org repo does not need a pin. Extend that
  list only for another same-org reference that must stay on `main`.
- Dependabot (`github-actions` ecosystem, monthly) bumps the SHA pins. Never
  "simplify" a SHA pin back to a tag.
- Unpinned `apt-get` and `choco` installs remain in some workflows. They are
  known gaps, not a precedent. Do not add more.
