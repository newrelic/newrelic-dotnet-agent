:: Copyright 2020 New Relic Corporation. All rights reserved.
:: SPDX-License-Identifier: Apache-2.0

SETLOCAL EnableExtensions

:: Bypass installing the agent and server monitor if locally emulating
IF "%EMULATED%" EQU "true" GOTO:EOF

FOR /F "usebackq tokens=1,2 delims==" %%i in (`wmic os get LocalDateTime /VALUE 2^>NUL`) do IF '.%%i.'=='.LocalDateTime.' SET ldt=%%j
SET ldt=%ldt:~0,4%-%ldt:~4,2%-%ldt:~6,2% %ldt:~8,2%:%ldt:~10,2%:%ldt:~12,6%

SET NR_ERROR_LEVEL=0

SET NR_IDCOUNT=0
:SET_NR_INSTALL_ID
SET NR_INSTALLID=%RANDOM%
IF EXIST "%RoleRoot%\nr-%NR_INSTALLID%.log" (
	IF %NR_IDCOUNT% LEQ 50 (
		SET /A NR_IDCOUNT=NR_IDCOUNT+1
		GOTO:SET_NR_INSTALL_ID
	) ELSE (
		ECHO Warning: Unable to find a free ID for MSI install logs, overwriting ID %NR_INSTALLID%. 
	)
)

CALL:INSTALL_NEWRELIC_AGENT

IF %NR_ERROR_LEVEL% EQU 0 (
	EXIT /B 0
) ELSE (
	EXIT %NR_ERROR_LEVEL%
)

:: --------------
:: Functions
:: --------------
:INSTALL_NEWRELIC_AGENT
	ECHO %ldt% : Begin installing the New Relic .NET Agent. >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1

	:: Current version of the installer
	SET NR_INSTALLER_NAME=AGENT_INSTALLER
	:: Path used for custom configuration and worker role environment varibles
	SET NR_HOME=%ALLUSERSPROFILE%\New Relic\.NET Agent\

	ECHO Installing the New Relic .NET Agent. >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1

	IF "%IsWorkerRole%" EQU "true" (
	    msiexec.exe /i %NR_INSTALLER_NAME% /norestart /quiet NR_LICENSE_KEY=%LICENSE_KEY% INSTALLLEVEL=50 /lv* %RoleRoot%\nr_install-%NR_INSTALLID%.log
	) ELSE (
	    msiexec.exe /i %NR_INSTALLER_NAME% /norestart /quiet NR_LICENSE_KEY=%LICENSE_KEY% /lv* %RoleRoot%\nr_install-%NR_INSTALLID%.log
	)

	:: WEB ROLES : Restart the service to pick up the new environment variables
	:: 	if we are in a Worker Role then there is no need to restart W3SVC _or_
	:: 	if we are emulating locally then do not restart W3SVC
	IF "%IsWorkerRole%" EQU "false" (
		ECHO Restarting IIS to pick up the new environment variables. >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1
		IISRESET /STOP
		IISRESET /START
	)

	IF %ERRORLEVEL% EQU 0 (
	  REM  The New Relic .NET Agent installed ok and does not need to be installed again.
	  ECHO New Relic .NET Agent was installed successfully. >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1

	) ELSE (
	  REM   An error occurred. Log the error to a separate log and exit with the error code.
	  ECHO  An error occurred installing the New Relic .NET Agent 1. Errorlevel = %ERRORLEVEL%. >> "%RoleRoot%\nr_error-%NR_INSTALLID%.log" 2>&1

	  SET NR_ERROR_LEVEL=%ERRORLEVEL%
	)

	ECHO Checking for registry keys after installation >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1
	REG QUERY HKLM\SYSTEM\CurrentControlSet\Services\W3SVC /v Environment >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1
	REG QUERY HKLM\SYSTEM\CurrentControlSet\Services\WAS /v Environment >> "%RoleRoot%\nr-%NR_INSTALLID%.log" 2>&1

GOTO:EOF
