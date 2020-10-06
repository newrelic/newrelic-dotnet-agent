#FROM microsoft/mssql-server-windows-developer:2019-latest
#FROM mcr.microsoft.com/windows/servercore:10.0.17763.1457
# same as my Windows host:
#FROM mcr.microsoft.com/windows/servercore:10.0.19041.508 

FROM mcr.microsoft.com/windows/servercore:ltsc2019
#USER "NT AUTHORITY\System"
# RUN NET USER TESTUSER "password0TS" /ADD
# RUN NET LOCALGROUP "Administrators" "TESTUSER" /ADD
#USER "TESTUSER"

ENV sql_express_download_url "https://go.microsoft.com/fwlink/?linkid=829176"
ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=password0TS
ENV ATTACH_DBS='[{"dbName":"NewRelic","dbFiles":["NewRelic.mdf","NewRelic_log.ldf"]}]'

#COPY NewRelic.mdf .
#COPY NewRelic_log.ldf .
COPY NewRelicDB.bak .
COPY restore.sql .
COPY start.ps1 /

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /

# RUN Invoke-WebRequest -Uri $env:sql_express_download_url -OutFile .\sqlexpress.exe ; \
#         Start-Process -Wait -FilePath .\sqlexpress.exe -ArgumentList /qs, /x:setup ; \
#         .\setup\setup.exe /q /ACTION=Install /INSTANCENAME=SQLEXPRESS /FEATURES=SQLEngine /UPDATEENABLED=0 /SQLSVCACCOUNT='NT AUTHORITY\System' /SQLSYSADMINACCOUNTS='BUILTIN\ADMINISTRATORS' /TCPENABLED=1 /NPENABLED=0 /IACCEPTSQLSERVERLICENSETERMS ; \
#         Remove-Item -Recurse -Force sqlexpress.exe, setup

#ADD https://go.microsoft.com/fwlink/?linkid=829176 sqlexpress.exe
COPY sqlexpress.exe .

RUN Start-Process -Wait -Verbose -FilePath .\sqlexpress.exe -ArgumentList '/qs /x:setup' ;
#RUN .\sqlexpress.exe -ArgumentList '/qs /x:setup' ;
#RUN .\setup\setup.exe /q /ACTION=Install /INSTANCENAME=SQLEXPRESS /FEATURES=SQLEngine /UPDATEENABLED=0 /SQLSYSADMINACCOUNTS='BUILTIN\ADMINISTRATORS' /TCPENABLED=1 /NPENABLED=0 /IACCEPTSQLSERVERLICENSETERMS ; 
#RUN .\setup\setup.exe /q /ACTION=Install /INSTANCENAME=SQLEXPRESS /FEATURES=SQLEngine /UPDATEENABLED=0 /SQLSVCACCOUNT='TESTUSER' /SQLSVCPASSWORD="password0TS" /SQLSYSADMINACCOUNTS='BUILTIN\ADMINISTRATORS' /TCPENABLED=1 /NPENABLED=0 /IACCEPTSQLSERVERLICENSETERMS ; 
#RUN .\setup\setup.exe /q /ACTION=Install /INSTANCENAME=SQLEXPRESS /FEATURES=SQLEngine /UPDATEENABLED=0 /SQLSVCACCOUNT='NT AUTHORITY\System' /SQLSYSADMINACCOUNTS='BUILTIN\ADMINISTRATORS' /TCPENABLED=1 /NPENABLED=0 /IACCEPTSQLSERVERLICENSETERMS ; 
RUN .\setup\setup.exe /q /ACTION=Install /INSTANCENAME=SQLEXPRESS /FEATURES=SQLEngine /UPDATEENABLED=0 /SQLSVCACCOUNT='containeradministrator' /SQLSYSADMINACCOUNTS='BUILTIN\ADMINISTRATORS' /TCPENABLED=1 /NPENABLED=0 /IACCEPTSQLSERVERLICENSETERMS ; 
#RUN Remove-Item -Recurse -Force sqlexpress.exe, setup

#RUN stop-service MSSQL`$SQLEXPRESS ; \
# RUN net start MSSQL`$SQLEXPRESS /m ; \
#         sqlcmd -E -S. -Q 'CREATE LOGIN [USER MANAGER\ContainerAdministrator] FROM WINDOWS WITH DEFAULT_DATABASE=[master], DEFAULT_LANGUAGE=[us_english]' ; \
#         sqlcmd -E -S. -Q 'ALTER SERVER ROLE [sysadmin] ADD MEMBER [USER MANAGER\ContainerAdministrator]' ; \
#         stop-service MSSQL`$SQLEXPRESS ;

RUN stop-service MSSQL`$SQLEXPRESS ; \
         set-itemproperty -path 'HKLM:\software\microsoft\microsoft sql server\mssql14.SQLEXPRESS\mssqlserver\supersocketnetlib\tcp\ipall' -name tcpdynamicports -value '' ; \
         set-itemproperty -path 'HKLM:\software\microsoft\microsoft sql server\mssql14.SQLEXPRESS\mssqlserver\supersocketnetlib\tcp\ipall' -name tcpport -value 1433 ; \
         set-itemproperty -path 'HKLM:\software\microsoft\microsoft sql server\mssql14.SQLEXPRESS\mssqlserver\' -name LoginMode -value 2 ;

# CMD .\start.ps1 -sa_password $env:SA_PASSWORD -ACCEPT_EULA $env:ACCEPT_EULA -attach_dbs $env:ATTACH_DBS -Verbose
CMD .\start.ps1 -sa_password $env:SA_PASSWORD -ACCEPT_EULA $env:ACCEPT_EULA -Verbose
