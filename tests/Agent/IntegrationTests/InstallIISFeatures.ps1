Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-WebServerRole
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-ASPNET45 -All
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-ApplicationInit
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-HttpTracing
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-BasicAuthentication
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-IPSecurity
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-URLAuthorization
Enable-WindowsOptionalFeature -Online -NoRestart -FeatureName IIS-WindowsAuthentication

Write-Host "`r`nDone.`r`n`r`nNOTE: You probably need to restart your computer before IIS will function correctly.`r`n`r`n"