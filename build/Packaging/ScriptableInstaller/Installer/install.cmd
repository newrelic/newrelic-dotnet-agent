@ECHO OFF
REM install.cmd
REM Collects and validates the user parameters before calling powershell with install.ps1
REM Usage: install.cmd -LicenseKey <String> [-InstallPath "<String>"] [-NoIisReset] [-InstrumentAll]

SETLOCAL EnableDelayedExpansion EnableExtensions

SET mypath=%~dp0

REM Check for no arguments and echo usage if missing.
IF [%1] EQU [] (
    ECHO Usage: install.cmd -LicenseKey ^<String^> [-InstallPath ^"^<String^>^"] [-NoIisReset] [-InstrumentAll]
    ECHO Note: Please make sure to use double-quotes around the InstallPath as shown above.
    Exit /B 13
    REM 13 = Data is invalid
)

ECHO %date:~4% %time% Starting install

REM Clear variables prior to work and set defaults as appropriate
SET _nr_licensekey=
SET _nr_installpath="%ProgramFiles%\New Relic\.Net Agent"
SET _nr_noiisreset=
SET _nr_instrumentall=
SET _nr_forcelicensekey=

REM Detect and set the arguments
:loop
IF NOT "%~1" EQU "" (
    IF /I "%~1" EQU "-LicenseKey" (
        SET _nr_licensekey=%~2
        SHIFT
    )
    IF /I "%~1" EQU "-InstallPath" (
        SET _nr_installpath="%~2"
        SHIFT
    )
    IF /I "%~1" EQU "-ForceLicenseKey" (
        SET _nr_forcelicensekey=-ForceLicenseKey
    )
    IF /I "%~1" EQU "-NoIisReset" (
        SET _nr_noiisreset=-NoIisReset
    )
    IF /I "%~1" EQU "-InstrumentAll" (
        SET _nr_instrumentall=-InstrumentAll
    )
    SHIFT
    GOTO :loop
)

REM Validate the arguments and provide defaults if appropriate
IF "%_nr_licensekey%" EQU "" (
    ECHO %date:~4% %time% ERROR: Please provide a license key to install the agent.
    EXIT /B 13
)
IF %_nr_installpath% EQU "" (
    ECHO %date:~4% %time% INFO: Missing install path, defaulting to "%ProgramFiles%\New Relic\.Net Agent".
    SET _nr_installpath="%ProgramFiles%\New Relic\.Net Agent"
)
IF "%_nr_noiisreset%" EQU "" (
    ECHO %date:~4% %time% INFO: Post install iisreset enabled.
) ELSE (
    ECHO %date:~4% %time% INFO: Post install iisreset disabled.
)
IF "%_nr_instrumentall%" EQU "" (
    ECHO %date:~4% %time% INFO: Instrument All disabled.
) ELSE (
    ECHO %date:~4% %time% INFO: Instrumentall enabled.
)
IF "%_nr_forcelicensekey%" EQU "" (
    ECHO %date:~4% %time% INFO: ForceLicenseKey disabled.
) ELSE (
    ECHO %date:~4% %time% INFO: ForceLicenseKey enabled.
)

REM Execute the powershell script with the validated parameters and with the ExecutionPolicy set to Bypass
ECHO %date:~4% %time% INFO: powershell.exe -ExecutionPolicy Bypass -File %mypath%\install.ps1 -LicenseKey %_nr_licensekey% -InstallPath %_nr_installpath% %_nr_noiisreset% %_nr_instrumentall%  %_nr_forcelicensekey%
powershell.exe -ExecutionPolicy Bypass -File "%mypath%\install.ps1" -LicenseKey %_nr_licensekey% -InstallPath %_nr_installpath% %_nr_noiisreset% %_nr_instrumentall% %_nr_forcelicensekey%
ECHO:

REM Report the results of the installation (this file and the powershell script)
if %ERRORLEVEL% NEQ 0 GOTO LABEL_ERROR

GOTO LABEL_END

:LABEL_ERROR
ENDLOCAL
SET EXITCODE=1 
SET EXITMESSAGE=%date:~4% %time% INFO: Installation failed with error code %EXITCODE%.
ECHO %EXITMESSAGE%
GOTO LABEL_EXIT

:LABEL_END
ENDLOCAL
SET EXITCODE=0
SET EXITMESSAGE=%date:~4% %time% INFO: Installation completed successfully.
ECHO %EXITMESSAGE%
GOTO LABEL_EXIT

:LABEL_EXIT
REM Cleanup env vars
SET _nr_licensekey=
SET _nr_installpath=
SET _nr_noiisreset=
SET _nr_instrumentall=
EXIT /B %EXITCODE%
