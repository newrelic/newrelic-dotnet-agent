$ErrorActionPreference = "Stop"
$packageName = "NewRelicWindowsAzure"

# Annotate the build
[Xml]$xml = Get-Content "$packageName.nuspec"
$version = $xml.package.metadata.version
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$version" -Method POST -Headers @{'Authorization'=$authorization} -MaximumRedirection 0 -ErrorVariable invokeErr -ErrorAction SilentlyContinue
if($invokeErr[0].FullyQualifiedErrorId.Contains("MaximumRedirectExceeded")){
    $null
}

# Execute the tests
cd .\nuget-test\
$setupSuccessful = $false
$retryCount = 0

while($setupSuccessful -eq $false -and $retryCount -lt 10)
{
    Try
    {
        . .\setup.ps1
        $setupSuccessful = $true
        Break
    }
    Catch
    {
        $ErrorMessage = $_.Exception.Message
        Write-Host "Caught an exception: $ErrorMessage"
        $retryCount++
        Write-Host "Retry count is now: $retryCount"
    }
}
Invoke-Pester -EnableExit -OutputXml ".\TestResults.xml"