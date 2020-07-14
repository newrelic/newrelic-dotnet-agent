# Delete the contents of 'C:\IntegrationTestWorkingDirectory'
Write-Host "Purging the contents of 'C:\IntegrationTestWorkingDirectory'"
Remove-Item -Path C:\IntegrationTestWorkingDirectory\* -Recurse -Force -ErrorAction SilentlyContinue
