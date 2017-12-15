$ErrorActionPreference = "Stop"
$packageName = "NewRelic.Azure.WebSites"

# Annotate the build
if ($env:Repository.Contains("x64"))
{
    $packageName = "NewRelic.Azure.WebSites.x64"
}

[Xml]$xml = Get-Content "$packageName.nuspec"
$annotation = "$($xml.package.metadata.version) - $env:Repository"
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$annotation" -Method POST -Headers @{'Authorization'=$authorization}

# Execute the tests
cd .\nuget.test\
$setupSuccessful = $false
$retryCount = 0

while($setupSuccessful -eq $false -and $retryCount -lt 10)
{
    Try
    {
        . .\setup.ps1
        #. "$env:windir\syswow64\Windowspowershell\v1.0\powershell.exe" .\setup.ps1
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