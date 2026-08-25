using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure.Identity;
using LifeLinkLanka.Infrastructure.Persistence;
using LifeLinkLanka.Infrastructure.Storage;
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
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IFileStorageService, SupabaseFileStorageService>();

        return services;
    }
}