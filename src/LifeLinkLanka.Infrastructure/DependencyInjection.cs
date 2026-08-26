using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure.Identity;
using LifeLinkLanka.Infrastructure.Persistence;
using LifeLinkLanka.Infrastructure.Services;
using LifeLinkLanka.Infrastructure.Storage;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeLinkLanka.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("MySqlConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connStr, ServerVersion.AutoDetect(connStr),
                mySqlOptions => mySqlOptions.EnableRetryOnFailure(3)));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false; // flip to true once real SMTP is wired
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IFileStorageService, SupabaseFileStorageService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IDonorEligibilityJob, DonorEligibilityJob>();

        services.AddHangfire(hfConfig => hfConfig
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(connStr, new MySqlStorageOptions
            {
                TransactionIsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                PrepareSchemaIfNecessary = true
            })));

        services.AddHangfireServer();

        return services;
    }
}