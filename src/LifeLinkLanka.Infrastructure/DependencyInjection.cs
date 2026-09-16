using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure.Identity;
using LifeLinkLanka.Infrastructure.Persistence;
using LifeLinkLanka.Infrastructure.Services;
using LifeLinkLanka.Infrastructure.Storage;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeLinkLanka.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connStr = GetMySqlConnectionString(config);
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(connStr, serverVersion,
                mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null)));

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
        services.AddScoped<IBloodMatchingService, BloodMatchingService>();

        services.AddHangfire(hfConfig => hfConfig
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMemoryStorage());

        services.AddHangfireServer();

        return services;
    }

    public static string GetMySqlConnectionString(IConfiguration config)
    {
        // 1. Try standard ConnectionStrings:MySqlConnection (or ConnectionStrings__MySqlConnection)
        var connStr = config.GetConnectionString("MySqlConnection");

        // 2. Try common environment variables used by cloud hosts (Render, Railway, Heroku)
        var envVar = Environment.GetEnvironmentVariable("MYSQL_URL")
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL")
                  ?? Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
                  ?? Environment.GetEnvironmentVariable("MYSQLCONNECTIONSTRING");

        if (!string.IsNullOrWhiteSpace(envVar))
        {
            connStr = ConvertUrlToMySqlConnectionString(envVar);
        }

        if (!string.IsNullOrWhiteSpace(connStr) && 
            (connStr.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase) || connStr.StartsWith("mysqlx://", StringComparison.OrdinalIgnoreCase)))
        {
            connStr = ConvertUrlToMySqlConnectionString(connStr);
        }

        return connStr ?? "Server=localhost;Port=3306;Database=lifelinklanka;User=root;Password=;";
    }

    public static string ConvertUrlToMySqlConnectionString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        if (raw.StartsWith("mysql://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("mysqlx://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(raw);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
                var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 3306;
                var database = uri.AbsolutePath.TrimStart('/');

                var sslMode = "Preferred";
                if (!string.IsNullOrWhiteSpace(uri.Query))
                {
                    var query = uri.Query.TrimStart('?');
                    var parts = query.Split('&');
                    foreach (var part in parts)
                    {
                        var kv = part.Split('=');
                        if (kv.Length == 2 && kv[0].Equals("ssl-mode", StringComparison.OrdinalIgnoreCase))
                        {
                            sslMode = kv[1];
                        }
                    }
                }

                return $"Server={host};Port={port};Database={database};User={user};Password={pass};SslMode={sslMode};AllowUserVariables=true;";
            }
            catch
            {
                return raw;
            }
        }

        return raw;
    }
}