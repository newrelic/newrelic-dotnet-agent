Param(
    [string] $productName
)

Import-Module -Name "$(Split-Path -Parent $PSCommandPath)\common\common.psm1" -Force
Import-Module -Name "$(Split-Path -Parent $PSCommandPath)\common\gitHub.psm1" -Force

$commitHash=(Get-Content Build\BuildArtifacts\_buildProperties\commithash.txt)

$product = Get-ProductInfo -productName $productName

Write-Host "Creating tags for: $product"

$version = $product.Version
$releaseTag = "$($product.GitTagPrefix)r$($product.Version)"

Write-Host "Running CreateTag $releaseTag Tagging release $version $commitHash"
CreateTag $releaseTag "Tagging release '$version'" "$commitHash"

$majorMinor = $version.Split(".")
$minorNext = [System.Convert]::ToInt16($majorMinor[1]) + 1
$nextVersion = $majorMinor[0] + "." + $minorNext
$nextVersion

$nextVersionTag = "$($product.GitTagPrefix)v$nextVersion"

$commit = GetCommit $commitHash
$date = $commit.commit.author.date

$commits = MakeAPIRequest "https://source.datanerd.us/api/v3/repos/dotNetAgent/dotnet_agent/commits?sha=dev&since=$date"
if ($commits.Count -gt 1)
{
    $nextCommit = $commits[0].sha
    Write-Host "Attempting to tag"
    CreateTag "$nextVersionTag" "Tagging $nextVersionTag" "$nextCommit"
}
else
{
    Write-Host "There are no commits on 'dev' following '$sha'."
}