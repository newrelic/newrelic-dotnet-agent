SETLOCAL EnableExtensions

:: Bypass installing the agent and server monitor if locally emulating
IF "%EMULATED%" EQU "true" GOTO:EOF

for /F "usebackq tokens=1,2 delims==" %%i in (`wmic os get LocalDateTime /VALUE 2^>NUL`) do if '.%%i.'=='.LocalDateTime.' set ldt=%%j
set ldt=%ldt:~0,4%-%ldt:~4,2%-%ldt:~6,2% %ldt:~8,2%:%ldt:~10,2%:%ldt:~12,6%

SET NR_ERROR_LEVEL=0

CALL:INSTALL_NEWRELIC_AGENT
CALL:INSTALL_NEWRELIC_SERVER_MONITOR

IF %NR_ERROR_LEVEL% EQU 0 (
	EXIT /B 0
) ELSE (
	EXIT %NR_ERROR_LEVEL%
)

:: --------------
:: Functions
:: --------------
:INSTALL_NEWRELIC_AGENT
	ECHO %ldt% : Begin installing the New Relic .net Agent >> "%RoleRoot%\nr.log" 2>&1

	:: Current version of the installer
	SET NR_INSTALLER_NAME=AGENT_INSTALLER
	:: Path used for custom configuration and worker role environment varibles
	SET NR_HOME=%ALLUSERSPROFILE%\New Relic\.NET Agent\

	ECHO Installing the New Relic .net Agent. >> "%RoleRoot%\nr.log" 2>&1

	IF "%IsWorkerRole%" EQU "true" (
	    msiexec.exe /i %NR_INSTALLER_NAME% /norestart /quiet NR_LICENSE_KEY=%LICENSE_KEY% INSTALLLEVEL=50 /lv* %RoleRoot%\nr_install.log
	) ELSE (
	    msiexec.exe /i %NR_INSTALLER_NAME% /norestart /quiet NR_LICENSE_KEY=%LICENSE_KEY% /lv* %RoleRoot%\nr_install.log
	)

	:: WEB ROLES : Restart the service to pick up the new environment variables
	:: 	if we are in a Worker Role then there is no need to restart W3SVC _or_
	:: 	if we are emulating locally then do not restart W3SVC
	IF "%IsWorkerRole%" EQU "false" (
		ECHO Restarting IIS and W3SVC to pick up the new environment variables >> "%RoleRoot%\nr.log" 2>&1
		IISRESET
		NET START W3SVC
	)

	IF %ERRORLEVEL% EQU 0 (
	  REM  The New Relic .net Agent installed ok and does not need to be installed again.
	  ECHO New Relic .net Agent was installed successfully. >> "%RoleRoot%\nr.log" 2>&1

	) ELSE (
	  REM   An error occurred. Log the error to a separate log and exit with the error code.
	  ECHO  An error occurred installing the New Relic .net Agent 1. Errorlevel = %ERRORLEVEL%. >> "%RoleRoot%\nr_error.log" 2>&1

	  SET NR_ERROR_LEVEL=%ERRORLEVEL%
	)

GOTO:EOF

:INSTALL_NEWRELIC_SERVER_MONITOR
	ECHO %ldt% : Begin installing the New Relic Server Monitor >> "%RoleRoot%\nr_server.log" 2>&1

	:: Current version of the installer
	SET NR_INSTALLER_NAME=SERVERMONITOR_INSTALLER

	ECHO Installing the New Relic Server Monitor. >> "%RoleRoot%\nr_server.log" 2>&1
	msiexec.exe /i %NR_INSTALLER_NAME% /norestart /quiet NR_LICENSE_KEY=%LICENSE_KEY% /lv* %RoleRoot%\nr_server_install.log

	IF %ERRORLEVEL% EQU 0 (
	  REM  The New Relic Server Monitor installed ok and does not need to be installed again.
	  ECHO New Relic Server Monitor was installed successfully. >> "%RoleRoot%\nr_server.log" 2>&1

	  NET STOP "New Relic Server Monitor Service"
	  NET START "New Relic Server Monitor Service"

	) ELSE (
	  REM   An error occurred. Log the error to a separate log and exit with the error code.
	  ECHO  An error occurred installing the New Relic Server Monitor 1. Errorlevel = %ERRORLEVEL%. >> "%RoleRoot%\nr_server_error.log" 2>&1

	  SET NR_ERROR_LEVEL=%ERRORLEVEL%
	)

GOTO:EOF
