Write-Host "Importing common.psm1"

function New-Product()
{
    $product = New-Object PSObject
    $product | Add-Member -Type NoteProperty -Name ArtifactoryRootFolder -Value ""
    $product | Add-Member -Type NoteProperty -Name PathsToArchive -Value ""
    $product | Add-Member -Type NoteProperty -Name Version -Value ""
    $product | Add-Member -Type NoteProperty -Name GitTagPrefix -Value ""
    return $product
}

$BuildDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\..\.."
$ToolsDirectory = Resolve-Path "$BuildDirectory\Tools"
$BuildArtifactsDirectory = Resolve-Path "$BuildDirectory\BuildArtifacts"
$BuildPropertiesDirectory = Resolve-Path "$BuildArtifactsDirectory\_buildProperties"

$ArtifactoryExe = Resolve-Path "$ToolsDirectory\Artifactory\jfrog.exe"
$7ZipExe = Resolve-Path "$ToolsDirectory\7-Zip\7z.exe"
$NuGetExe = Resolve-Path "$ToolsDirectory\NuGet\nuget.exe"

$CommitHash = Get-Content "$BuildPropertiesDirectory\commithash.txt"

$agent = New-Product
$agent.ArtifactoryRootFolder = "agent"
$agent.PathsToArchive = "**/*"
$agent.Version = Get-Content "$BuildPropertiesDirectory\version_agent.txt"
$agent.GitTagPrefix = ""

$lambdaopentracer = New-Product
$lambdaopentracer.ArtifactoryRootFolder = "NewRelic.OpenTracing.AmazonLambda.Tracer"
$lambdaopentracer.PathsToArchive = "Build/BuildArtifacts/NugetAwsLambdaOpenTracer/* AwsLambda/**/* Agent/_build/AnyCPU-Release/NewRelic.Agent.Core/**/* Build/BuildArtifacts/_buildProperties/*"
$lambdaopentracer.Version = Get-Content "$BuildPropertiesDirectory\version_lambdaopentracer.txt"
$lambdaopentracer.GitTagPrefix = "AwsLambdaOpenTracer_"

$azuresiteextension = New-Product
$azuresiteextension.ArtifactoryRootFolder = "AzureSiteExtension"
$azuresiteextension.PathsToArchive = "Build/BuildArtifacts/AzureSiteExtension/* Build/BuildArtifacts/_buildProperties/*"
$azuresiteextension.Version = Get-Content "$BuildPropertiesDirectory\version_azuresiteextension.txt"
$azuresiteextension.GitTagPrefix = "AzureSiteExtension_"

$products = @{
    Agent = $agent;
    LambdaOpenTracer = $lambdaopentracer;
    AzureSiteExtension = $azuresiteextension
}

function Get-ProductInfo([Parameter(Mandatory=$true)][string] $productName)
{
    $product = $products[$productName]
    if ($null -eq $product) { throw "No product info found for: $productName" }
    $product
}

Export-ModuleMember -Function Get-ProductInfo
Export-ModuleMember -Variable @("ArtifactoryExe","7ZipExe","NuGetExe")