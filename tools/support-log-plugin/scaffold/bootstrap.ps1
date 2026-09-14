# Preflight for the nr-agents marketplace. Reads only; writes nothing.
$ErrorActionPreference = 'Stop'

$hostName = 'source.datanerd.us'

$py = $null
foreach ($candidate in @('python', 'python3')) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        $py = $candidate
        break
    }
}
if (-not $py -and (Get-Command 'py' -ErrorAction SilentlyContinue)) {
    $py = 'py'
}
if (-not $py) {
    Write-Host 'Python 3 is required and was not found as python, python3, or py.'
    Write-Host 'Install it from https://www.python.org/downloads/ and run this again.'
    exit 1
}
Write-Host "python: $(& $py --version 2>&1)"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host 'git is required and was not found.'
    exit 1
}
Write-Host "git: $(git --version)"

git config --get-all credential.helper > $null 2>&1
$anyHelper = ($LASTEXITCODE -eq 0)
if (-not $anyHelper) {
    git config --get "credential.https://$hostName.helper" > $null 2>&1
    $anyHelper = ($LASTEXITCODE -eq 0)
}
if (-not $anyHelper) {
    Write-Host "No git credential helper is configured for $hostName."
    Write-Host 'Set one up once, then run this again:'
    Write-Host "  gh auth login --hostname $hostName"
    Write-Host "  gh auth setup-git --hostname $hostName"
    exit 1
}
Write-Host "git credentials: a helper is configured for $hostName"

Write-Host ''
Write-Host 'This machine is ready. Run these two lines in Claude Code:'
Write-Host ''
Write-Host '  /plugin marketplace add https://source.datanerd.us/agents/claude-skills.git'
Write-Host '  /plugin install dotnet-log-triage@nr-agents'
Write-Host ''
Write-Host 'Then set this in ~/.claude/settings.json under "env", so a failed'
Write-Host 'background refresh keeps your working copy instead of deleting it:'
Write-Host ''
Write-Host '  "CLAUDE_CODE_PLUGIN_KEEP_MARKETPLACE_ON_FAILURE": "1"'
Write-Host ''
Write-Host 'To pull an update later, run /plugin marketplace update nr-agents by hand.'
Write-Host ''
Write-Host 'This script changed nothing.'
