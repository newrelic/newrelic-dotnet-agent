$ErrorActionPreference = "Stop"
$packageName = "NewRelic.Agent.Api"

# Annotate the build
[Xml]$xml = Get-Content "$packageName.nuspec"
$version = $xml.package.metadata.version
$authorization = 'Basic ' + [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("msneeden:$env:JenkinsAPIToken"))
Invoke-RestMethod -Uri "$($env:BUILD_URL)submitDescription?description=$version" -Method POST -Headers @{'Authorization'=$authorization}

# Execute the tests
cd .\nuget-test\
$setupSuccessful = $false
$retryCount = 0

while($setupSuccessful -eq $false -and $retryCount -lt 10)
{
    Try
    {
        . .\setup.ps1
        # . "$env:windir\syswow64\Windowspowershell\v1.0\powershell.exe" .\setup.ps1
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