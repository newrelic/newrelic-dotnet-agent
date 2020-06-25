:: Copyright 2020 New Relic Corporation. All rights reserved.
:: SPDX-License-Identifier: Apache-2.0

@ECHO OFF
REM uninstall.cmd
REM Uninstalls the currently installed version of the .Net Agent
REM Usage: uninstall.cmd [-Force | -Rollback]

SETLOCAL EnableDelayedExpansion EnableExtensions

SET mypath=%~dp0

REM Checks if this is a cleanup from a failed install attempt. No Logging.
IF /I "%1" EQU "-Rollback" (
    GOTO :rollback
)

REM Prep logging
SET _nr_now=%date:~7,2%-%date:~4,2%-%date:~12,2%_%time:~3,2%
SET _nr_defaultPath="%ProgramData%\New Relic\Logs"
IF NOT EXIST %_nr_defaultPath% (
    MD %_nr_defaultPath%
)
SET _nr_logs="%_nr_defaultPath%\agent-uninstall.cmd_%_nr_now%.txt"
ECHO %date:~4% %time% Starting uninstall.cmd log > "%_nr_logs%"


REM Check for no arguments and prompt to uninstall since that requires an IISRESET
IF /I "%1" NEQ "-Force" (
    ECHO INFO: Uninstalling the .Net Agent REQUIRES an iisreset to release resources.
    ECHO INFO: To bypass this check, run uninstall.cmd -Force
    :iisreset
    SET /P check=Are you sure you want to continue with the IISRESET? [Y/N]
    if /I "!check!" EQU "Y" (
        ECHO %date:~4% %time% Selected to continue with iisreset and start uninstall >> "%_nr_logs%"
        GOTO :uninstall
    )
    if /I "!check!" EQU "N" (
        ECHO %date:~4% %time% ERROR: Selected to not perform the iisreset and abort the uninstall >> "%_nr_logs%"
        Exit /B 87  REM 87 = The parameter is incorrect
    )
    GOTO :iisreset
)
ECHO %date:~4% %time% INFO: Uninstall started using the -Force switch >> "%_nr_logs%"

:uninstall
ECHO %date:~4% %time% Uninstalling the .Net Agent
REM Execute the powershell script to uninstall and with the ExecutionPolicy set to Bypass
ECHO %date:~4% %time% INFO: powershell.exe -ExecutionPolicy Bypass -File "%mypath%\uninstall.ps1" >> "%_nr_logs%"
powershell.exe -ExecutionPolicy Bypass -File "%mypath%\uninstall.ps1"

REM Report the results of the installation (this file and the powershell script)
IF %ERRORLEVEL% EQU 0 (
    GOTO :success
) 
IF %ERRORLEVEL% EQU 3 (
    GOTO :complete3
)
GOTO :failure

:success
ECHO %date:~4% %time% INFO: Uninstall completed successfully. >> "%_nr_logs%"
ECHO %date:~4% %time% INFO: Uninstall completed successfully.
GOTO :EOF

:complete3
ECHO %date:~4% %time% INFO: Uninstall completed with code %ERRORLEVEL%, meaning the .Net Agent could not be found. >> "%_nr_logs%"
ECHO %date:~4% %time% INFO: Uninstall completed with code %ERRORLEVEL%, meaning the .Net Agent could not be found.
GOTO :EOF

:failure
ECHO %date:~4% %time% ERROR: Uninstall failed with error code %ERRORLEVEL%. >> "%_nr_logs%"
ECHO %date:~4% %time% ERROR: Uninstall failed with error code %ERRORLEVEL%.
GOTO :EOF

:rollback
REM removes most of the agent details and puts back the previous installation files/details
powershell.exe -ExecutionPolicy Bypass -File "%mypath%\uninstall.ps1" -Rollback
GOTO :EOF

:EOF
ENDLOCAL