@ECHO OFF

iisreset /stop

Echo Deleting x86 Temporary ASP.NET Files

for /d %%i in ("%systemroot%\Microsoft.Net\Framework\v*") do for /d %%f in ("%%i\Temporary ASP.NET Files\*") do RD /q/s "%%f"

Echo Deleting x64 Temporary ASP.NET Files
for /d %%i in ("%systemroot%\Microsoft.Net\Framework64\v*") do for /d %%f in ("%%i\Temporary ASP.NET Files\*") do RD /q/s "%%f"

iisreset /start

@ECHO ON