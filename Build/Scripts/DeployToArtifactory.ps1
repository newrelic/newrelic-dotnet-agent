Param(
    [Parameter(Mandatory=$true)][string] $user,
    [Parameter(Mandatory=$true)][string] $password,
    [Parameter(Mandatory=$true)][string] $productName
)

Import-Module -Name "$(Split-Path -Parent $PSCommandPath)\common" -Force

$product = Get-ProductInfo $productName

Write-Host "Uploading to Artifactory: $product"

$rt_url="https://artifacts.datanerd.us"
$rt_archiveName = (Get-Date -format "yyyyMMdd") +  "_r" + $product.Version
$rt_archiveName_tar = $rt_archiveName + ".tar"
$rt_archiveName_tgz = $rt_archiveName + ".tgz"
$rt_destination = "dotnet-release/$($product.ArtifactoryRootFolder)/r$($product.Version)/"

Invoke-Expression "& $7ZipExe a -ttar $rt_archiveName_tar $($product.PathsToArchive)"
Invoke-Expression "& $7ZipExe a -tgzip $rt_archiveName_tgz $rt_archiveName_tar"

Write-Host "Running $ArtifactoryExe rt upload --url=$rt_url --user=$user --password=$password --flat=false $rt_archiveName_tgz $rt_destination"

& $ArtifactoryExe rt upload --url=$rt_url --user=$user --password=$password --flat=false $rt_archiveName_tgz $rt_destination