@echo off

powershell.exe -ExecutionPolicy RemoteSigned -File install.ps1

echo %ERRORLEVEL%
