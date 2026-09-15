using FlowDesk.Application;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Logging;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Security;
using FlowDesk.Web.Authorization;
using FlowDesk.Web.Services;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration with Sensitive Data Redaction
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Destructure.With<SensitiveDataRedactionDestructuringPolicy>()
        .WriteTo.Console());

// Add Clean Architecture Layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Web Context & Current User Service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentWebUserService>();

// Permission Authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Authentication with Secure Cookie Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Name = "__Host-FlowDesk-Auth";
    });

// Controllers with Views (MVC) & Auto Anti-Forgery Protection
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// Auto-migration & Database Seeding on startup
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();

    // Register Recurring Background Jobs
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<IBackgroundJobService>("SendApprovalReminder", j => j.ExecuteApprovalRemindersAsync(CancellationToken.None), "*/15 * * * *");
    recurringJobManager.AddOrUpdate<IBackgroundJobService>("CheckExpiredApprovals", j => j.ExecuteCheckExpiredApprovalsAsync(CancellationToken.None), "*/30 * * * *");
    recurringJobManager.AddOrUpdate<IBackgroundJobService>("ProcessEscalations", j => j.ExecuteProcessEscalationsAsync(CancellationToken.None), Cron.Hourly());
    recurringJobManager.AddOrUpdate<IBackgroundJobService>("GenerateScheduledReports", j => j.ExecuteGenerateScheduledReportsAsync(CancellationToken.None), Cron.Daily());
    recurringJobManager.AddOrUpdate<IBackgroundJobService>("CleanupTemporaryFiles", j => j.ExecuteCleanupTemporaryFilesAsync(CancellationToken.None), Cron.Daily(2));
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Protected Hangfire Dashboard Endpoint
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
