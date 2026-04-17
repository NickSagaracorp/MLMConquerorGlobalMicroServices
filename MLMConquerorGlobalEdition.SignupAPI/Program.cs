using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Amazon.S3;
using AspNetCoreRateLimit;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using MLMConquerorGlobalEdition.SharedKernel.Logging;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Middleware;
using MLMConquerorGlobalEdition.SignupAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPiiMaskingConsole();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength         = 8;
    options.Password.RequireDigit           = true;
    options.Password.RequireUppercase       = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail         = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddSignInManager();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});


// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

// AWS S3
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var accessKey = builder.Configuration["AWS:Credentials:AccessKey"];
    var secretKey = builder.Configuration["AWS:Credentials:SecretKey"];
    var region    = builder.Configuration["AWS:S3:Region"] ?? "us-east-1";

    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        return new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));

    return new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
});
builder.Services.AddScoped<IS3FileService, S3FileService>();
builder.Services.AddScoped<ISponsorBonusService, SponsorBonusService>();

// JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Error Tracking
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

// Redis distributed cache + ICacheService
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();

// Firebase push notifications
builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

// HangFire
builder.Services.AddScoped<ProcessScheduledCancellationsJob>();
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("HangFire")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 5);
});

// Controllers only — no Blazor
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Return ApiResponse<T> on model-binding failures instead of ValidationProblemDetails
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToArray();
        var response = MLMConquerorGlobalEdition.SharedKernel.ApiResponse<object>
            .Fail("VALIDATION_ERROR", string.Join("; ", errors));
        response.Errors = errors;
        return new BadRequestObjectResult(response);
    };
});

// CORS — allow Signups frontend origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignupsFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? ["https://localhost:7147"];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var publicKeyBase64 = builder.Configuration["Jwt:PublicKeyBase64"]
    ?? throw new InvalidOperationException("Jwt:PublicKeyBase64 not configured.");

if (publicKeyBase64.StartsWith("REPLACE_WITH_") && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "JWT RSA public key must be set before running in non-Development mode.");

RsaSecurityKey? jwtValidationKey = null;
if (!publicKeyBase64.StartsWith("REPLACE_WITH_"))
{
    var rsaValidation = RSA.Create();
    rsaValidation.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
    jwtValidationKey = new RsaSecurityKey(rsaValidation);
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.UseSecurityTokenValidators = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = jwtValidationKey is not null,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = jwtValidationKey,
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror Signup API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed roles and root ambassador on startup
using (var scope = app.Services.CreateScope())
{
    var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var config      = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var seedLogger  = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    await RolesSeeder.SeedAsync(roleManager, seedLogger);
    await RootAmbassadorSeeder.SeedAsync(db, userManager, config, seedLogger);
    await CountriesSeeder.SeedAsync(db, seedLogger);
    await ProductsSeeder.SeedAsync(db, seedLogger);
    await CountryProductDefaultSeeder.SeedAsync(db, seedLogger);
}

app.UseStaticFiles();
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseCors("SignupsFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// HangFire
app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<ProcessScheduledCancellationsJob>(
    "process-scheduled-cancellations",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 1 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.SignupAPI",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

app.Run();
