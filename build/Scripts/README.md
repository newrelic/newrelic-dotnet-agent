# CI Scripts

The scripts in this folder are used by NR's internal CI.

Most of the scripts should be easy to place, but the information below should help with the more complex stuff.

**DotNet-Agent-CI-CleanInstallServersCombined-EC2.ps1**

This script is used  for both:

* DotNet-Agent-CI-CleanInstallServersPost-EC2
* DotNet-Agent-CI-CleanInstallServersPre-EC2

**get-job-build-params-push-to-parent.groovy**

This script replaces the groovy found in the following jobs:

* DotNet-Agent-ThinInstaller-CI-Build

**DotNet-Agent-ThinInstaller-CI-CleanInstallServersCombined-EC2.ps1**

* DotNet-Agent-ThinInstaller-CI-CleanInstallServersPost-EC2
* DotNet-Agent-ThinInstaller-CI-CleanInstallServersPre-EC2