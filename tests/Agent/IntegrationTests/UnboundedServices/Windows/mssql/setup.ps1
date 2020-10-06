Write-Output "setup.ps1"
# Starts a loop to check if SQL is ready before running setup script.
DO
{
    sqlcmd -S localhost -U sa -d master -Q "SET NOCOUNT ON; SELECT 1" -W -h -1
    Start-Sleep 1s
} WHILE ($LASTEXITCODE -ne 0)

.\sqlcmd -S localhost -U sa -i \restore.sql