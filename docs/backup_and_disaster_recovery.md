# FlowDesk Backup & Disaster Recovery Strategy

## 1. Overview & Operational Goals

FlowDesk Enterprise Platform maintains strict business continuity and data integrity SLAs. This document defines the backup schedule, restore verification protocol, target Recovery Point Objective (RPO), and Recovery Time Objective (RTO).

### Targets
- **Recovery Point Objective (RPO)**: < 1 Hour (Transaction log backups taken every 15 minutes; full backups taken daily).
- **Recovery Time Objective (RTO)**: < 2 Hours (Automated restoration scripts and containerized failover procedures).

---

## 2. Backup Strategy & Schedules

| Backup Type | Frequency | Retention Period | Target Storage Location |
| :--- | :--- | :--- | :--- |
| **Full Database Backup** | Daily at 02:00 UTC | 30 Days | Isolated Immutable Storage |
| **Differential Backup** | Every 6 Hours | 7 Days | Secondary Storage Volume |
| **Transaction Log Backup** | Every 15 Minutes | 72 Hours | High-speed Backup Directory |

---

## 3. Automated Backup & Verification Workflow

Automated backup execution and integrity verification can be triggered via PowerShell script:

### Full Backup Execution
```powershell
.\scripts\backup_and_restore.ps1 -Action Backup -ServerInstance "localhost" -DatabaseName "FlowDeskDb" -BackupPath "C:\Backups\FlowDesk"
```

### Verification Check
```powershell
.\scripts\backup_and_restore.ps1 -Action Verify -ServerInstance "localhost" -DatabaseName "FlowDeskDb" -BackupPath "C:\Backups\FlowDesk"
```

### Emergency Restoration Procedure
```powershell
.\scripts\backup_and_restore.ps1 -Action Restore -ServerInstance "localhost" -DatabaseName "FlowDeskDb" -BackupFile "C:\Backups\FlowDesk\FlowDeskDb_20260915_020000.bak"
```

---

## 4. Disaster Recovery Procedure Step-by-step

1. **Incidence Declaration & Isolation**:
   - Isolate impaired primary database node.
   - Redirect incoming HTTP traffic to maintenance response or read-only failover node.
2. **Retrieve Immutable Backup**:
   - Download the latest verified `.bak` file from primary/secondary off-site backup storage.
3. **Run Automated Restore Utility**:
   - Execute restore script against fresh/reprovisioned SQL Server instance.
4. **Execute Post-Restore Sanity Check**:
   - Run EF Core migrations check: `dotnet ef database update`.
   - Execute Health Check validation: `GET /health/ready`.
5. **Traffic Redirection**:
   - Update API connection string environment variables and restart API app service containers.
