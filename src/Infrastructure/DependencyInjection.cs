using System.Text;
using FlowDesk.Application.Common.Interfaces;
using FlowDesk.Infrastructure.Options;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Persistence.Interceptors;
using FlowDesk.Infrastructure.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FlowDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddScoped<AuditableEntityInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost\\SQLEXPRESS;Database=FlowDeskDb;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<FlowDeskDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(FlowDeskDbContext).Assembly.FullName));
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<FlowDeskDbContext>());

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IRequestNumberGenerator, RequestNumberGenerator>();
        services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();
        services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
        services.AddScoped<IWorkflowEvaluator, WorkflowEvaluator>();
        services.AddScoped<IApproverResolver, ApproverResolver>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<DatabaseSeeder>();

        // Phase 9: Caching & Redis Configuration
        var redisConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConn;
                options.InstanceName = "FlowDesk_";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Phase 7: Notifications & Background Processing Services
        services.AddTransient<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddTransient<IBackgroundJobService, HangfireBackgroundJobService>();

        // Hangfire Configuration with SQL Server Persistent Storage for Production, Memory fallback for testing
        services.AddHangfire(config =>
        {
            config.UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings();

            var hangfireConn = configuration.GetConnectionString("HangfireConnection") ?? connectionString;
            if (!string.IsNullOrWhiteSpace(hangfireConn) && !hangfireConn.Contains("InMemoryDatabase", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    config.UseSqlServerStorage(hangfireConn);
                }
                catch
                {
                    config.UseMemoryStorage();
                }
            }
            else
            {
                config.UseMemoryStorage();
            }
        });

        services.AddHangfireServer();

        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET");
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            secretKey = jwtOptions.Secret;
        }
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            secretKey = "FlowDesk_Dev_Default_Secret_Key_For_Local_Development_Only_Must_Be_32_Chars!";
        }

        services.AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = string.IsNullOrWhiteSpace(jwtOptions.Issuer) ? "FlowDesk.API" : jwtOptions.Issuer,
                    ValidAudience = string.IsNullOrWhiteSpace(jwtOptions.Audience) ? "FlowDesk.Clients" : jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}
