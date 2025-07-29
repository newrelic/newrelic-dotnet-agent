# PowerShell script to backup a SQL Server database running in a Docker container and copy the backup file to the host machine.
# This script assumes you have a SQL Server container running and a backup.sql file ready to execute.
#
# Usage: backup-database.ps1 -ContainerName "MssqlServer" -SqlUsername "sa" -SqlPassword "MssqlPassw0rd" -BackupPath "./backup" -BackupFileName "NewRelicDB.bak"

param(
    [string]$ContainerName = "MssqlServer",
    [string]$SqlUsername = "sa",
    [string]$SqlPassword = "MssqlPassw0rd",
    [string]$BackupPath = "./",
    [string]$BackupFileName = "NewRelicDB.bak"
)

# Ensure backup directory exists on host
if (!(Test-Path $BackupPath)) {
    New-Item -ItemType Directory -Path $BackupPath -Force
    Write-Host "Created backup directory: $BackupPath"
}

# Get the directory where this script is located
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackupSqlPath = Join-Path $ScriptDir "backup.sql"

# Check if backup.sql exists
if (!(Test-Path $BackupSqlPath)) {
    Write-Error "backup.sql file not found at: $BackupSqlPath"
    exit 1
}

Write-Host "Starting database backup process..."

try {
    # Check if container is running
    $containerStatus = docker ps --filter "name=$ContainerName" --format "{{.Status}}"
    if ([string]::IsNullOrEmpty($containerStatus)) {
        Write-Error "Container '$ContainerName' is not running"
        exit 1
    }
    Write-Host "Container '$ContainerName' is running"

    # Copy backup.sql to container
    Write-Host "Copying backup.sql to container..."
    docker cp $BackupSqlPath "${ContainerName}:/tmp/backup.sql"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to copy backup.sql to container"
    }

    # Create backup directory in container and set permissions
    Write-Host "Creating backup directory in container..."
    docker exec $ContainerName mkdir -p /var/opt/mssql/backup
    docker exec $ContainerName chown mssql:mssql /var/opt/mssql/backup/NewRelicDB.bak
    docker exec $ContainerName chmod 755 /var/opt/mssql/backup

    # Execute backup script using sqlcmd
    Write-Host "Executing backup script..."
    docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd -S localhost -U $SqlUsername -P $SqlPassword -No -i /tmp/backup.sql
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to execute backup script"
    }

    # Copy backup file from container to host
    Write-Host "Copying backup file from container to host..."
    $hostBackupPath = Join-Path $BackupPath $BackupFileName
    docker cp "${ContainerName}:/var/opt/mssql/backup/$BackupFileName" $hostBackupPath
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to copy backup file from container"
    }

    # Clean up temporary file in container
    docker exec $ContainerName rm -f /tmp/backup.sql

    Write-Host "Database backup completed successfully!"
    Write-Host "Backup file location: $hostBackupPath"
    
    # Display backup file size
    $backupFile = Get-Item $hostBackupPath
    Write-Host "Backup file size: $([math]::Round($backupFile.Length / 1MB, 2)) MB"

} catch {
    Write-Error "Error during backup process: $($_.Exception.Message)"
    exit 1
}
