using Asp.Versioning;
using FlowDesk.API.Authorization;
using FlowDesk.API.Middleware;
using FlowDesk.API.Services;
using FlowDesk.Application;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

// Add Clean Architecture Layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Presentation / API Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Permission Authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Controllers & Versioning
builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FlowDeskDbContext>("database");

// Swagger / OpenAPI with Bearer Token Authentication
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FlowDesk Enterprise API",
        Version = "v1",
        Description = "Production-ready Enterprise Workflow & Approval Management Platform API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token format: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
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

// Middleware Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlowDesk API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard Endpoint
app.UseHangfireDashboard("/hangfire");

// Map Health Checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
});

app.MapControllers();

app.Run();

public partial class Program { }
