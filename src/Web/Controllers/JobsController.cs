using FlowDesk.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

[Authorize]
public class JobsController : Controller
{
    private readonly IBackgroundJobService _jobService;

    public JobsController(IBackgroundJobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var jobs = new[]
        {
            new { Name = "SendApprovalReminder", Schedule = "Every 15 Minutes", Status = "Active", Description = "Scans pending approvals nearing SLA deadline and dispatches reminders." },
            new { Name = "CheckExpiredApprovals", Schedule = "Every 30 Minutes", Status = "Active", Description = "Scans pending approvals exceeding SLA limit." },
            new { Name = "ProcessEscalations", Schedule = "Every 1 Hour", Status = "Active", Description = "Escalates overdue approvals (>24h past SLA) to higher management." },
            new { Name = "GenerateScheduledReports", Schedule = "Daily at Midnight", Status = "Active", Description = "Generates system workflow summary reports for admins." },
            new { Name = "CleanupTemporaryFiles", Schedule = "Daily at 02:00 AM", Status = "Active", Description = "Removes temporary files older than 24 hours." }
        };

        ViewBag.Engine = "Hangfire Engine";
        ViewBag.SystemTime = DateTime.UtcNow;

        return View(jobs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trigger(string jobName)
    {
        try
        {
            switch (jobName?.ToLowerInvariant())
            {
                case "sendapprovalreminder":
                case "approvalreminder":
                    await _jobService.ExecuteApprovalRemindersAsync();
                    TempData["Success"] = "Job 'SendApprovalReminder' executed successfully.";
                    break;
                case "checkexpiredapprovals":
                case "expiredapprovals":
                    await _jobService.ExecuteCheckExpiredApprovalsAsync();
                    TempData["Success"] = "Job 'CheckExpiredApprovals' executed successfully.";
                    break;
                case "processescalations":
                case "escalations":
                    await _jobService.ExecuteProcessEscalationsAsync();
                    TempData["Success"] = "Job 'ProcessEscalations' executed successfully.";
                    break;
                case "generatescheduledreports":
                case "scheduledreports":
                    await _jobService.ExecuteGenerateScheduledReportsAsync();
                    TempData["Success"] = "Job 'GenerateScheduledReports' executed successfully.";
                    break;
                case "cleanuptemporaryfiles":
                case "cleanupfiles":
                    await _jobService.ExecuteCleanupTemporaryFilesAsync();
                    TempData["Success"] = "Job 'CleanupTemporaryFiles' executed successfully.";
                    break;
                default:
                    TempData["Error"] = $"Unknown job '{jobName}'.";
                    break;
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to execute job '{jobName}': {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
