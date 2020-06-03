# NewRelic.DotNet.Agent.Headless.Installer

### Overview

This addresses the need to install the .NET agent where using the traditional installer does not work for the given environment.  This also makes the automation and distribution of the agent more simplistic in extremely large enterprise environments. This method of installing the agent provides the user with the greatest amount of flexibility while still providing the standard functionality of the current agent where web applications will be automatically instrumented.

### This repository

This repository only contains the scripts and necessary tools for installing/uninstalling the agent. It does not contain the actual agent files. This installer is meant to be able to be run "offline", so in order to run it, you must download a full distribution from:

http://download.newrelic.com/dot_net_agent/release/scriptable_installer/NewRelic.Agent.Installer.zip

### Requirements
* Powershell V2 or higher
* IIS running
* The ability to "run as administrator" for a cmd

### Installing the agent (install.cmd)

Run the installer with the following command (must be an elevated administrator command prompt):

`install.cmd -licenseKey <your_license_key> [options]`

Additional options:

* `instrumentAll`
 * This option enables instrumentation of non-IIS applications. It does so by defining the following environment variables: COR_ENABLE_PROFILING, COR_PROFILER, and NEWRELIC_INSTALL_PATH.
* `noIISReset`
 * This option prevents the installer from performing an IIS reset after installation. IIS applications will not be instrumented until IIS has been restarted.
* `installPath`
 * This option allows the installation directory to be customized. The default install location is `%ProgramFiles%\New Relic`
* `forceLicenseKey`
 * This option allows reinstalling the agent with a new license key.

### Updating the agent (install.cmd)

The installed agent can be updated using `install.cmd` with all the same options outlined in the previous section. The installed agent can be upgraded or downgraded with the installer.

### Removing the agent (uninstall.cmd)

Run the installer with the following command (must be an elevated administrator command prompt):

`uninstall.cmd [options]`

* `force`
 * This option forces an IIS reset without prompting the user.
* `rollback`
 * This option suppresses logging to the console window. It is used by the installer script in the event of a failed install.

### Technical information

###### Registry keys

* The following values are added for the W3SVC and WAS service. These enable automatic instrumentation for IIS applications

      [HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\W3SVC]
      "Environment"=hex(7):<hex string representing profiler environment variables>

      [HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\WAS]
      "Environment"=hex(7):<hex string representing profiler environtment variables>

* These keys are added on both 32-bit and 64-bit machines (Paths will differ when `installPath` install option is used).

      [HKEY_LOCAL_MACHINE\SOFTWARE\New Relic]

      [HKEY_LOCAL_MACHINE\SOFTWARE\New Relic\.Net Agent]
      "NewRelicHome"="C:\\ProgramData\\New Relic\\.NET Agent\\"

      [HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}]
      @="New Relic .Net Profiler"

      [HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InProcServer32]
      @="C:\\Program Files\\New Relic\\.Net Agent\\NewRelic.Profiler.dll"
      "ThreadingModel"="Both"

      [HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\Version]
      @="AGENT_VERSION_STRING"

* These keys are added to only 64-bit machines enabling instrumentation of 32-bit apps (Paths will differ when `installPath` install option is used).

      [HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\New Relic]

      [HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\New Relic\.Net Agent]
      "NewRelicHome"="C:\\ProgramData\\New Relic\\.NET Agent\\"

      [HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}]
      @="New Relic .Net Profiler"

      [HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32]
      @="C:\\Program Files\\New Relic\\.Net Agent\\x86\\NewRelic.Profiler.dll"
      "ThreadingModel"="Both"

      [HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\Version]
      @="AGENT_VERSION_STRING"

###### Installed files

TODO: Add file hierarchy


###### Environment variables
When `instrumentAll` is used, the following environment variables are defined.
* `COR_ENABLE_PROFILING`
 * This is set to `1` indicating to the system that processes should use the profiler defined by the `COR_PROFILER` environment variable.
* `COR_PROFILER`
 * This is set to the CLSID key of the New Relic profiler: {71DA0A04-7777-4EC6-9643-7D28B46A8A41}. It indicates to the system which profiler to use when `COR_ENABLE_PROFILING` is enabled.
* `NEWRELIC_INSTALL_PATH`
 * This is set to the install location of the agent.

### Troubleshooting

##### Administrator access required
Many errors during install or uninstall can occur due to not running from an elevated command prompt. The installer requires this in order to create directories, set directory permissions, create registry keys, and set environment variables. The install script attempts to auto-elevate, but in the event there are any issues it is good to verify this.

###### ERROR: The installer requires .NET 4.0 to be installed.
NuGet.exe is contained within the installer package. Nuget.exe requires .NET 4.0. The installer uses NuGet.exe behind the scenes to unpack the agent files, so is therefore dependent on .NET 4.0.

###### INFO: You do not appear to have .NET 3.5 installed. You will not be able to instrument your .NET 2.0/3.0 applications until .NET 3.5 is installed.
This installer does not require .NET 3.5. However, this message will display in the event that .NET 3.5 is not installed. Instrumenting applications running on the .NET 2.0 CLR with the agent requires .NET 3.5 be installed. Applications running on the .NET 4.0 CLR remain unaffected and will be instrumented.

###### INFO: IISReset not performed.  This will need to be done before you are able instrument your web applications.
This is not an error, but is displayed when the `noIISReset` option is used. A manual IIS reset will need to be performed before any IIS applications will get instrumented.

###### ERROR: A version of the New Relic agent was previously installed on this machine using the MSI installer. Please use the MSI to remove this installation before attempting to install via the New Relic Thin Installer.
The install of the agent with the MSI installer versus this installer can differ slightly. Customers previously using the MSI installer need to uninstall using the MSI prior to migrating to this installer.

###### ERROR: You are attempting to upgrade the agent to a new directory. Please uninstall the current agent first.
When using the `installPath` option, if a new path is chosen different than was used in a previous install, then an uninstall must be performed first.

###### ERROR: The license key you have entered differs from the one used in the previous install. If you wish to overwrite the license key please re-run this installer with the -forceLicenceKey flag.
When reinstalling the agent with a new license key, the `forceLicenceKey` option is required in order to avoid accidental changes to the license key during an reinstall or upgrade.

###### Note about uninstalling
A number of the error messages indicate that an uninstall must be performed before proceeding. Keep in mind that uninstalling removes the `newrelic.config`. Its a good idea to back this up if customizations have been made.
