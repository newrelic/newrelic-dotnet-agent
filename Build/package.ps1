Param(
  [Parameter(Mandatory=$True,Position=1)]
  [string]$package,
  [Parameter(Mandatory=$False)]
  [string]$configuration,
  [Parameter(Mandatory=$False)]
  [string]$platform,
  [Parameter(Mandatory=$False)]
  [string]$version,
  [Parameter(Mandatory=$False)]
  [switch]$pushNugetPackage
)
$artifactBuilderProject = "ArtifactBuilder\ArtifactBuilder.csproj"

if ($package.ToLower() -eq "nugetazurewebsites")
{
    if ($pushNugetPackage)
    {
        dotnet run --project $artifactBuilderProject $package $configuration $platform -pushNugetPackage
    }
    else
    {
        dotnet run --project $artifactBuilderProject $package $configuration $platform
    }
}

elseif ($package.ToLower() -eq "azuresiteextension")
{
    dotnet run --project $artifactBuilderProject $package $version
}

else
{
    Throw "Invalid package"
}

exit $LastExitCode