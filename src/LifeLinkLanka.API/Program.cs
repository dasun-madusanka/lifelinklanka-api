using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using LifeLinkLanka.API.Hubs;
using LifeLinkLanka.API.Middleware;
using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Application.Validators;
using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/lifelinklanka-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Convert.FromBase64String(builder.Configuration["Jwt:Secret"]!)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role"   
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VerifiedHospitalOnly", policy =>
        policy.RequireRole(Roles.HospitalStaff).RequireClaim("mfaEnabled", "True"));
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LifeLink Lanka API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    var allowedOrigins = new List<string>
    {
        "http://localhost:4200",
        "https://localhost:4200",
        "http://localhost:3000",
        "http://127.0.0.1:4200",
        "https://lifelinklanka-web.vercel.app"
    };

    var customOrigin = builder.Configuration["Cors:AllowedOrigin"];
    if (!string.IsNullOrWhiteSpace(customOrigin))
    {
        var split = customOrigin.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in split)
        {
            var trimmed = s.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !allowedOrigins.Contains(trimmed))
            {
                allowedOrigins.Add(trimmed);
            }
        }
    }

    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Database Migration & Seeding with Resilience and Render Cloud Diagnostics
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var connStr = LifeLinkLanka.Infrastructure.DependencyInjection.GetMySqlConnectionString(config);
    var isRender = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RENDER"));

    const int maxAttempts = 5;

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            Log.Information("Connecting to MySQL and applying pending migrations (Attempt {Attempt}/{MaxAttempts})...", attempt, maxAttempts);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully.");

            Log.Information("Seeding default identity roles and system administrators...");
            await LifeLinkLanka.Infrastructure.Identity.IdentitySeeder.SeedAllAsync(app.Services);
            Log.Information("Identity seeding completed.");

            break;
        }
        catch (Exception ex)
        {
            Log.Warning("Database connection attempt {Attempt}/{MaxAttempts} failed: {ErrorMessage}", attempt, maxAttempts, ex.Message);
            if (attempt < maxAttempts)
            {
                Log.Information("Waiting 3 seconds before next connection attempt...");
                await Task.Delay(3000);
            }
            else
            {
                Log.Error(ex, "FATAL: Could not establish connection to MySQL database after {MaxAttempts} attempts.", maxAttempts);
                
                if (isRender || connStr.Contains("localhost", StringComparison.OrdinalIgnoreCase) || connStr.Contains("YOUR_DB_PASSWORD"))
                {
                    Log.Fatal(@"
====================================================================================
RENDER CLOUD DEPLOYMENT CONFIGURATION REQUIRED:
------------------------------------------------------------------------------------
Your application is running in a cloud/container environment without a local MySQL server.
You must configure your cloud MySQL database connection string in the Render Dashboard:

1. Open Render Dashboard: https://dashboard.render.com
2. Select your 'lifelinklanka-api' Web Service.
3. Click 'Environment' in the left menu.
4. Add the following Environment Variable:
   Key:   ConnectionStrings__MySqlConnection
   Value: Server=<YOUR_HOST>;Port=<PORT>;Database=<DB_NAME>;User=<USER>;Password=<PASSWORD>;SslMode=Preferred;

(Alternatively set 'MYSQL_URL' or 'DATABASE_URL' if using a connection URL).
====================================================================================");
                }

                if (app.Environment.IsProduction())
                {
                    throw;
                }
            }
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();

// Ensure wwwroot/uploads exists for verification documents
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}
app.UseStaticFiles();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new LifeLinkLanka.API.HangfireDashboardAuthFilter() }
});

app.MapControllers();
app.MapHub<EmergencyHub>("/hubs/emergency");

RecurringJob.AddOrUpdate<IDonorEligibilityJob>(
    "donor-eligibility-recalculation",
    job => job.RecalculateAllDonorsAsync(),
    Cron.Daily);

app.Run();
