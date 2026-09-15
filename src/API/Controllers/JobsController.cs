using FlowDesk.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[Authorize]
public class JobsController : ApiControllerBase
{
    private readonly IBackgroundJobService _jobService;

    public JobsController(IBackgroundJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet("status")]
    public IActionResult GetJobsStatus()
    {
        var jobs = new[]
        {
            new { Name = "SendApprovalReminder", Schedule = "Every 15 Minutes", Status = "Active", Description = "Scans pending approvals nearing SLA deadline and dispatches reminders." },
            new { Name = "CheckExpiredApprovals", Schedule = "Every 30 Minutes", Status = "Active", Description = "Scans pending approvals exceeding SLA limit." },
            new { Name = "ProcessEscalations", Schedule = "Every 1 Hour", Status = "Active", Description = "Escalates overdue approvals (>24h past SLA) to higher management." },
            new { Name = "GenerateScheduledReports", Schedule = "Daily at Midnight", Status = "Active", Description = "Generates system workflow summary reports for admins." },
            new { Name = "CleanupTemporaryFiles", Schedule = "Daily at 02:00 AM", Status = "Active", Description = "Removes temporary files older than 24 hours." }
        };

        return Ok(new
        {
            SystemTime = DateTime.UtcNow,
            Engine = "Hangfire Background Processing Engine",
            ActiveJobs = jobs
        });
    }

    [HttpPost("trigger/{jobName}")]
    [Authorize(Policy = "Permissions.System.Settings")]
    public async Task<IActionResult> TriggerJob(string jobName)
    {
        switch (jobName.ToLowerInvariant())
        {
            case "sendapprovalreminder":
            case "approvalreminder":
                await _jobService.ExecuteApprovalRemindersAsync();
                break;
            case "checkexpiredapprovals":
            case "expiredapprovals":
                await _jobService.ExecuteCheckExpiredApprovalsAsync();
                break;
            case "processescalations":
            case "escalations":
                await _jobService.ExecuteProcessEscalationsAsync();
                break;
            case "generatescheduledreports":
            case "scheduledreports":
                await _jobService.ExecuteGenerateScheduledReportsAsync();
                break;
            case "cleanuptemporaryfiles":
            case "cleanupfiles":
                await _jobService.ExecuteCleanupTemporaryFilesAsync();
                break;
            default:
                return BadRequest(new { Message = $"Unknown job name '{jobName}'. Supported jobs: SendApprovalReminder, CheckExpiredApprovals, ProcessEscalations, GenerateScheduledReports, CleanupTemporaryFiles" });
        }

        return Ok(new { Message = $"Job '{jobName}' triggered successfully.", ExecutedAt = DateTime.UtcNow });
    }
}
