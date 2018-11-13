$ErrorActionPreference = "Stop"
$msBuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe"
$testTarget = "http://dotnet-integration-cfp-core20-pcf-app.apps.pcf.datanerd.us/"
$utilString = "http://dotnet-integration-cfp-core20-pcf-app.apps.pcf.datanerd.us/logs/utilizationstring"

# Builds and Publishes the Application
function BuildApp() {
    Write-Host "Building Application and Publishing"
    cd $env:WORKSPACE\DotNet-Integration-CFP-Core20-PCF-App\
    c:\nuget3.exe restore -SolutionDirectory .\
    dotnet restore
    . $msBuildPath DotNet-Integration-CFP-Core20-PCF-App.csproj
    Write-Host "Completed Building Application and Publishing"
}

# Pushes the Application to PCF
function PushApp() {
    Write-Host "Pushing Application to PCF"
    cd $env:WORKSPACE\DotNet-Integration-CFP-Core20-PCF-App\bin\Release\PublishOutput\
    cf push
    Write-Host "Completed Pushing Application to PCF"
}

# Exercise App
function ExerciseApp() {
    Write-Host "Excercising App"
    Invoke-WebRequest -Uri "$testTarget" -UseBasicParsing
}

# Get the Connect string from the App
function GetConnectionStringFromApp() {
    $connectInfo = Invoke-WebRequest -Uri "$utilString" -UseBasicParsing
    return $connectInfo.Content
}

# Validate the Vendor data
function ValidateConnectData($connectData) {
    $data = ConvertFrom-Json $connectData

    if(($data.utilization.vendors.pcf.cf_instance_guid -ne $null) -and ($data.utilization.vendors.pcf.cf_instance_ip -ne $null) -and ($data.utilization.vendors.pcf.memory_limit -ne $null))
    {
        return $true
    }
    return $false
}

# Run the tests and report results.
function RunTests() {
    Write-Host "Executing Test"
    ExerciseApp
    $connectString = GetConnectionStringFromApp
    $valid = ValidateConnectData($connectString)

    if ($valid -eq $true) {
        $scriptExitCode = 0;
        Write-Host "Success"
    
    }
    else {
        Write-Host "Failure"
    }
    
    Write-Host "Script Exit Code: $scriptExitCode"
    $LastExitCode = $scriptExitCode 
}