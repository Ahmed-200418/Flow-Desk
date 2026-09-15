# FlowDesk Enterprise Platform - Backup & Restore Automation Script
# Usage: .\backup_and_restore.ps1 -Action Backup -ServerInstance "localhost" -DatabaseName "FlowDeskDb" -BackupPath "C:\Backups\FlowDesk"

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("Backup", "Restore", "Verify")]
    [string]$Action,

    [Parameter(Mandatory = $false)]
    [string]$ServerInstance = "localhost",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseName = "FlowDeskDb",

    [Parameter(Mandatory = $false)]
    [string]$BackupPath = "C:\Backups\FlowDesk",

    [Parameter(Mandatory = $false)]
    [string]$BackupFile = ""
)

$ErrorActionPreference = "Stop"

function Ensure-Directory {
    param ([string]$Path)
    if (-not (Test-Path -Path $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
        Write-Host "[INFO] Created backup directory: $Path" -ForegroundColor Green
    }
}

switch ($Action) {
    "Backup" {
        Ensure-Directory -Path $BackupPath
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $targetFile = Join-Path -Path $BackupPath -ChildPath "${DatabaseName}_${timestamp}.bak"

        Write-Host "[BACKUP] Starting full database backup for '$DatabaseName'..." -ForegroundColor Cyan
        $sqlQuery = "BACKUP DATABASE [$DatabaseName] TO DISK = N'$targetFile' WITH CHECKSUM, COMPRESSION, STATS = 10;"

        try {
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $sqlQuery -ErrorAction Stop
            Write-Host "[SUCCESS] Backup completed successfully: $targetFile" -ForegroundColor Green
        } catch {
            Write-Host "[FALLBACK] SQLCmd cmdlet unavailable or failed. Using sqlcmd.exe standard utility..." -ForegroundColor Yellow
            sqlcmd -S $ServerInstance -Q $sqlQuery
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[SUCCESS] Backup completed successfully via sqlcmd utility." -ForegroundColor Green
            } else {
                Write-Error "[FAILURE] Database backup operation failed."
            }
        }
    }

    "Verify" {
        if ([string]::IsNullOrWhiteSpace($BackupFile)) {
            $latestFile = Get-ChildItem -Path $BackupPath -Filter "*.bak" | Sort-CreationTime -Descending | Select-Object -First 1
            if ($null -eq $latestFile) {
                Write-Error "[FAILURE] No .bak backup files found in $BackupPath"
                return
            }
            $BackupFile = $latestFile.FullName
        }

        Write-Host "[VERIFY] Verifying integrity of backup file: $BackupFile" -ForegroundColor Cyan
        $sqlQuery = "RESTORE VERIFYONLY FROM DISK = N'$BackupFile' WITH CHECKSUM;"

        try {
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $sqlQuery -ErrorAction Stop
            Write-Host "[SUCCESS] Backup verification succeeded! File is valid and restorable." -ForegroundColor Green
        } catch {
            sqlcmd -S $ServerInstance -Q $sqlQuery
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[SUCCESS] Backup verification succeeded via sqlcmd utility." -ForegroundColor Green
            } else {
                Write-Error "[FAILURE] Backup file integrity check failed!"
            }
        }
    }

    "Restore" {
        if ([string]::IsNullOrWhiteSpace($BackupFile)) {
            Write-Error "[FAILURE] You must specify -BackupFile parameter for Restore action."
            return
        }

        Write-Host "[RESTORE] Restoring database '$DatabaseName' from '$BackupFile'..." -ForegroundColor Yellow
        $sqlQuery = @"
ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$DatabaseName] FROM DISK = N'$BackupFile' WITH REPLACE, RECOVERY;
ALTER DATABASE [$DatabaseName] SET MULTI_USER;
"@

        try {
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Query $sqlQuery -ErrorAction Stop
            Write-Host "[SUCCESS] Database restoration completed successfully!" -ForegroundColor Green
        } catch {
            sqlcmd -S $ServerInstance -Q $sqlQuery
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[SUCCESS] Database restoration completed successfully via sqlcmd utility." -ForegroundColor Green
            } else {
                Write-Error "[FAILURE] Database restore operation failed!"
            }
        }
    }
}
