############################################################
# Copyright 2026 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

<#
.SYNOPSIS
Authenticode-signs, or verifies the signatures of, New Relic's own Windows binaries in the
agent home directories, before they are packaged.

.DESCRIPTION
Selects every NewRelic*.dll and NewRelic*.exe under src\Agent\newrelichome_* (the managed
agent, its extensions and the Windows profiler) plus the two NewRelic.Api.Agent.dll builds under
src\_build, which ArtifactBuilder and the MSI take from there instead of from a home directory.
Every other PE file in a home directory must be a known third-party binary, or the script fails,
so a new New Relic binary with a different name can't ship unsigned unnoticed.

-Action Sign signs the selection with the certificate in Cert:\CurrentUser\My, using SHA-256 and
an RFC 3161 timestamp, so the signatures stay valid after the certificate expires.
-Action Verify fails unless every selected file has a valid signature from that certificate,
chaining to a trusted root, with a timestamp.

Run it after FullAgent.sln is built and before the MSI build and the "homefolders" upload, so
the MSI, ZIP, NuGet and Linux packages all carry the signed files. Only release_build.yml runs
it: unsigned PR builds (all_solutions.yml) never import a certificate and skip it.

.PARAMETER Action
Sign or Verify.

.PARAMETER CertificateThumbprints
Comma-separated thumbprints of the certificates imported from the signing PFX into
Cert:\CurrentUser\My. Exactly one of them must be a code-signing certificate with a private key;
that one signs, and Verify requires it as the signer.

.PARAMETER Configuration
Build configuration of FullAgent.sln (selects src\_build\AnyCPU-<Configuration>).

.PARAMETER TimestampUrl
RFC 3161 timestamp server. Defaults to the issuing CA's (DigiCert).
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Sign', 'Verify')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprints,

    [string]$Configuration = 'Release',

    [string]$TimestampUrl = 'http://timestamp.digicert.com',

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

# Third-party PE files that ship in the home directories, signed (or not) by their publisher.
$thirdPartyBinaries = @('grpc_csharp_ext.x64.dll', 'grpc_csharp_ext.x86.dll')

$signBatchSize = 20
$maxAttempts = 4
$codeSigningOid = '1.3.6.1.5.5.7.3.3'

function Get-SigningCertificate {
    $candidates = foreach ($thumbprint in ($CertificateThumbprints -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        $cert = Get-Item -Path "Cert:\CurrentUser\My\$thumbprint" -ErrorAction SilentlyContinue
        if ($cert -and $cert.HasPrivateKey -and ($cert.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq $codeSigningOid })) {
            $cert
        }
    }

    $candidates = @($candidates)
    if ($candidates.Count -ne 1) {
        throw "Expected exactly one code-signing certificate with a private key among the given thumbprints, found $($candidates.Count)."
    }

    Write-Host "Signing certificate: $($candidates[0].Subject), issued by $($candidates[0].Issuer), thumbprint $($candidates[0].Thumbprint), expires $($candidates[0].NotAfter.ToString('u'))"
    return $candidates[0]
}

function Get-SignTool {
    $kitsBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $signTool = Get-ChildItem -Path $kitsBin -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -as [version] } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $signTool) {
        throw "signtool.exe not found under $kitsBin."
    }

    return $signTool
}

function Get-FilesToSign {
    $agentDir = Join-Path $RepoRoot 'src\Agent'
    $homeDirs = @(Get-ChildItem -Path $agentDir -Directory -Filter 'newrelichome_*')
    if ($homeDirs.Count -eq 0) {
        throw "No newrelichome_* directories under $agentDir. Build FullAgent.sln first."
    }

    $selected = New-Object System.Collections.Generic.List[string]
    $unexpected = New-Object System.Collections.Generic.List[string]

    foreach ($homeDir in $homeDirs) {
        $peFiles = @(Get-ChildItem -Path $homeDir.FullName -Recurse -File |
            Where-Object { $_.Extension -in '.dll', '.exe' -and $_.FullName -notmatch '\\logs\\' })

        $homeSelection = @($peFiles | Where-Object { $_.Name -like 'NewRelic*' })
        foreach ($file in $peFiles) {
            if ($file.Name -notlike 'NewRelic*' -and $thirdPartyBinaries -notcontains $file.Name) {
                $unexpected.Add($file.FullName)
            }
        }

        if (-not ($homeSelection | Where-Object { $_.Name -eq 'NewRelic.Agent.Core.dll' })) {
            throw "$($homeDir.Name) has no NewRelic.Agent.Core.dll; the home directory is incomplete."
        }

        # The Windows homes carry the native profiler; the Linux homes carry libNewRelicProfiler.so,
        # which isn't a PE file and can't be Authenticode-signed.
        if ($homeDir.Name -notlike '*_linux' -and -not ($homeSelection | Where-Object { $_.Name -eq 'NewRelic.Profiler.dll' })) {
            throw "$($homeDir.Name) has no NewRelic.Profiler.dll."
        }

        Write-Host "$($homeDir.Name): $($homeSelection.Count) of $($peFiles.Count) PE files selected"
        $homeSelection | ForEach-Object { $selected.Add($_.FullName) }
    }

    if ($unexpected.Count -gt 0) {
        throw "PE files in the home directories that are neither NewRelic* nor a known third-party binary. Rename them or add them to the third-party list:`n$($unexpected -join "`n")"
    }

    # ArtifactBuilder (NuGet packages) and the MSI take the public API assembly from the build output.
    foreach ($framework in 'net462', 'netstandard2.0') {
        $apiDll = Join-Path $RepoRoot "src\_build\AnyCPU-$Configuration\NewRelic.Api.Agent\$framework\NewRelic.Api.Agent.dll"
        if (-not (Test-Path $apiDll)) {
            throw "$apiDll not found. Build FullAgent.sln ($Configuration) first."
        }

        $selected.Add($apiDll)
    }

    Write-Host "Selected $($selected.Count) files."
    return , $selected.ToArray()
}

function Invoke-Sign([string[]]$files, $certificate) {
    $signTool = Get-SignTool
    Write-Host "Using $signTool, timestamp server $TimestampUrl"

    for ($i = 0; $i -lt $files.Count; $i += $signBatchSize) {
        $batch = @($files[$i..([Math]::Min($i + $signBatchSize, $files.Count) - 1)])
        $signArgs = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', '/d', 'New Relic .NET Agent',
            '/s', 'My', '/sha1', $certificate.Thumbprint) + $batch

        for ($attempt = 1; ; $attempt++) {
            & $signTool @signArgs
            if ($LastExitCode -eq 0) {
                break
            }

            # Re-signing replaces the signature on files the failed attempt already signed.
            if ($attempt -ge $maxAttempts) {
                throw "signtool failed with exit code $LastExitCode after $attempt attempts."
            }

            $delay = 10 * $attempt
            Write-Host "::warning::signtool failed with exit code $LastExitCode (attempt $attempt of $maxAttempts), most likely a timestamp server error. Retrying in $delay seconds."
            Start-Sleep -Seconds $delay
        }
    }

    Write-Host "Signed $($files.Count) files."
}

function Invoke-Verify([string[]]$files, $certificate) {
    $failures = New-Object System.Collections.Generic.List[string]
    foreach ($file in $files) {
        $signature = Get-AuthenticodeSignature -FilePath $file
        if ($signature.Status -ne 'Valid') {
            $failures.Add("$file : $($signature.Status) $($signature.StatusMessage)")
        }
        elseif ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            $failures.Add("$file : signed by $($signature.SignerCertificate.Subject) ($($signature.SignerCertificate.Thumbprint)), not the release certificate")
        }
        elseif (-not $signature.TimeStamperCertificate) {
            $failures.Add("$file : not timestamped")
        }
    }

    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) {
            Write-Host "::error::$failure"
        }

        throw "$($failures.Count) of $($files.Count) files are not validly signed and timestamped."
    }

    Write-Host "Verified $($files.Count) files: valid signature from $($certificate.Subject), timestamped."
}

$certificate = Get-SigningCertificate
$files = Get-FilesToSign

if ($Action -eq 'Sign') {
    Invoke-Sign $files $certificate
}
else {
    Invoke-Verify $files $certificate
}
