############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

# Pester tests for Content\install.ps1. The tests parse install.ps1 and load only its
# function definitions, so the install logic at the bottom of the script does not run.

BeforeAll {
    $script:InstallScript = (Resolve-Path (Join-Path $PSScriptRoot '..\Content\install.ps1')).Path

    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($script:InstallScript, [ref]$null, [ref]$parseErrors)
    if ($parseErrors) { throw "install.ps1 has parse errors: $parseErrors" }
    $functions = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)
    foreach ($function in $functions) { . ([scriptblock]::Create($function.Extent.Text)) }
}

Describe 'NewSignatureValidationConfig' {
    BeforeEach {
        Mock WriteToInstallLog { }
        Mock InvokeNuget { return 0 }
        $script:ConfigPath = Join-Path $TestDrive 'nuget.signing.config'
    }

    It 'seeds a config that requires signatures and pins the newrelic owner' {
        NewSignatureValidationConfig $script:ConfigPath | Should -BeTrue
        Should -Invoke InvokeNuget -Times 1 -Exactly -ParameterFilter { $arguments -like '*-Owners newrelic *' }
        Get-Content $script:ConfigPath -Raw | Should -Match 'signatureValidationMode" value="require"'
    }

    It 'treats a nuget failure exit code as no config, retries once, logs the WARNING, and removes the file' {
        Mock InvokeNuget { return 1 }
        NewSignatureValidationConfig $script:ConfigPath | Should -BeFalse
        Should -Invoke InvokeNuget -Times 2 -Exactly
        Should -Invoke WriteToInstallLog -ParameterFilter { $output -like 'WARNING:*' } -Times 1 -Exactly
        Test-Path $script:ConfigPath | Should -BeFalse
    }
}

Describe 'InvokeNugetInstall' {
    BeforeEach {
        Mock WriteToInstallLog { }
        Mock InvokeNuget { return 0 }
        Mock StopInstall { throw "stop: $message" }
    }

    It 'stops when the package folder already exists, because nuget would skip signature validation' {
        New-Item -ItemType Directory -Path (Join-Path $TestDrive 'out\Pkg.1.2.3') -Force | Out-Null
        { InvokeNugetInstall 'Pkg' '1.2.3' 'src' (Join-Path $TestDrive 'out') $null } | Should -Throw '*already exists*'
        Should -Invoke InvokeNuget -Times 0 -Exactly
    }

    It 'passes -ConfigFile to nuget when a config path is given' {
        InvokeNugetInstall 'Pkg' '1.2.3' 'src' (Join-Path $TestDrive 'out2') 'C:\cfg\nuget.signing.config' | Should -Be 0
        Should -Invoke InvokeNuget -ParameterFilter { $arguments -like '*-ConfigFile "C:\cfg\nuget.signing.config"*' } -Times 1 -Exactly
    }

    It 'omits -ConfigFile when there is no config path' {
        InvokeNugetInstall 'Pkg' '1.2.3' 'src' (Join-Path $TestDrive 'out3') $null | Should -Be 0
        Should -Invoke InvokeNuget -ParameterFilter { $arguments -notlike '*-ConfigFile*' } -Times 1 -Exactly
    }
}

Describe 'install.ps1 end to end (child process, fake nuget on PATH)' {
    BeforeAll {
        # Runs install.ps1 in a child process against a fake nuget; the fake's behavior comes from FAKE_NUGET_MODE.
        function Invoke-InstallScenario($name, $mode, $version = '10.57.0') {
            $root = Join-Path $TestDrive $name
            $ext = Join-Path $root 'ext'
            $homeDir = Join-Path $root 'home'
            $bin = Join-Path $root 'bin'
            $framework = Join-Path $homeDir 'NewRelicAgent\Framework'
            New-Item -ItemType Directory -Path $ext, $bin, (Join-Path $framework 'extensions') -Force | Out-Null

            $contentDir = Split-Path -Parent $script:InstallScript
            Copy-Item "$contentDir\install.ps1", "$contentDir\applicationHost.xdt" -Destination $ext
            Set-Content (Join-Path $framework 'agent.dll') 'old'
            Set-Content (Join-Path $framework 'newrelic.config') '<configuration />'
            $poolConfig = Join-Path $root 'applicationHost.config'
            Set-Content $poolConfig '<configuration><system.applicationHost><applicationPools><add name="p" managedRuntimeVersion="v4.0" /></applicationPools></system.applicationHost></configuration>'

            # The fake logs each call, then fails or fakes a package download depending on FAKE_NUGET_MODE.
            # Before 8.17.438 the package has only the Framework content, with no contentFiles folder.
            Set-Content (Join-Path $bin 'nuget.cmd') '@echo %*>>"%~dp0nuget.calls"', '@powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0fake-nuget.ps1" %*', '@exit /b %ERRORLEVEL%' -Encoding Ascii
            Set-Content (Join-Path $bin 'fake-nuget.ps1') @'
if ($args[0] -eq 'trusted-signers') { exit 0 }
if ($env:FAKE_NUGET_MODE -eq 'fail') { exit 1 }
$out = $args[[array]::IndexOf($args, '-OutputDirectory') + 1]
$version = $args[[array]::IndexOf($args, '-Version') + 1]
$package = Join-Path $out "$($args[1]).$version"
$folders = @('content\newrelic')
if ([version]$version -ge [version]'8.17.438') { $folders += 'contentFiles\any\netstandard2.0\newrelic' }
foreach ($relative in $folders) {
    $dir = Join-Path $package $relative
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    Set-Content (Join-Path $dir 'newrelic.config') '<configuration><log level="info" /></configuration>'
    Set-Content (Join-Path $dir 'agent.dll') 'new'
}
exit 0
'@

            $vars = @{
                PATH = "$bin;$env:PATH"; HOME = $homeDir; WEBROOT_PATH = (Join-Path $root 'wwwroot'); APP_POOL_CONFIG = $poolConfig
                NEW_RELIC_AGENT_VERSION_OVERRIDE = $version; NEW_RELIC_LICENSE_KEY = 'test'; FAKE_NUGET_MODE = $mode
                FUNCTIONS_WORKER_RUNTIME = $null; NEWRELIC_AGENT_VERSION_OVERRIDE = $null
            }
            $savedEnv = @{}
            foreach ($var in $vars.Keys) { $savedEnv[$var] = [Environment]::GetEnvironmentVariable($var) }

            $pushed = $false
            try {
                foreach ($var in $vars.Keys) { [Environment]::SetEnvironmentVariable($var, $vars[$var]) }

                # Kudu runs install.ps1 from the extension folder, and the script uses paths relative to it.
                Push-Location $ext
                $pushed = $true
                & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ext 'install.ps1') | Out-Null
                $exitCode = $LASTEXITCODE
            }
            finally {
                if ($pushed) { Pop-Location }
                foreach ($var in $savedEnv.Keys) { [Environment]::SetEnvironmentVariable($var, $savedEnv[$var]) }
            }

            @{
                ExitCode  = $exitCode
                Log       = Get-Content (Join-Path $ext 'install.log') -Raw
                Calls     = @(Get-Content (Join-Path $bin 'nuget.calls'))
                Framework = $framework
                Core      = Join-Path $homeDir 'NewRelicAgent\Core'
                Staging   = Join-Path $ext 'NewRelicPackage'
            }
        }
    }

    Context 'nuget install fails' {
        BeforeAll { $script:Fail = Invoke-InstallScenario 'fail' 'fail' }

        It 'exits 1 and logs the failed install' {
            $script:Fail.ExitCode | Should -Be 1
            $script:Fail.Log | Should -Match 'nuget install NewRelic\.Agent 10\.57\.0 failed with exit code 1'
        }

        It 'passes the signing config to the nuget install' {
            @($script:Fail.Calls | Where-Object { $_ -like 'install *-ConfigFile*' }).Count | Should -Be 1
        }

        It 'leaves the existing agent files in place' {
            Get-Content (Join-Path $script:Fail.Framework 'agent.dll') | Should -Be 'old'
        }
    }

    Context 'nuget install succeeds' {
        BeforeAll { $script:Pass = Invoke-InstallScenario 'pass' 'pass' }

        It 'exits 0 and removes the staging folder' {
            $script:Pass.ExitCode | Should -Be 0
            Test-Path $script:Pass.Staging | Should -BeFalse
        }

        It 'installs the new agent into the Framework and Core folders' {
            Get-Content (Join-Path $script:Pass.Framework 'agent.dll') | Should -Be 'new'
            Get-Content (Join-Path $script:Pass.Core 'agent.dll') | Should -Be 'new'
        }

        It 'keeps the previous Framework file as .save' {
            Get-Content (Join-Path $script:Pass.Framework 'agent.dll.save') | Should -Be 'old'
        }

        It 'restores the customer newrelic.config after the upgrade' {
            Get-Content (Join-Path $script:Pass.Framework 'newrelic.config') | Should -Be '<configuration />'
        }
    }

    Context 'nuget install succeeds for a version before 8.17.438' {
        BeforeAll { $script:Old = Invoke-InstallScenario 'old' 'pass' '8.0.0' }

        It 'exits 0 and installs only the Framework agent' {
            $script:Old.ExitCode | Should -Be 0
            Get-Content (Join-Path $script:Old.Framework 'agent.dll') | Should -Be 'new'
            Test-Path $script:Old.Core | Should -BeFalse
        }

        It 'logs that the Core agent is not installed' {
            $script:Old.Log | Should -Match 'does not install \.NET Core Agent version less than 8\.17\.438'
        }

        It 'downloads one NewRelic.Azure.WebSites package' {
            $installs = @($script:Old.Calls | Where-Object { $_ -like 'install *' })
            $installs.Count | Should -Be 1
            $installs[0] | Should -Match '^install NewRelic\.Azure\.WebSites(\.x64)? -Version 8\.0\.0 '
        }
    }
}
