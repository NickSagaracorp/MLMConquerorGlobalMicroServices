using System.Text;
using Amazon.S3;
using AspNetCoreRateLimit;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Jobs;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.RankEngine.Middleware;
using MLMConquerorGlobalEdition.RankEngine.Services;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR — scans all handlers in this assembly + error-handling pipeline behavior
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});


// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

// Error Tracking — singleton; uses IServiceScopeFactory for isolated DB writes
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

// Register GetRankProgressHandler as scoped so EvaluateRankHandler can inject it
builder.Services.AddScoped<GetRankProgressHandler>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

builder.Services.AddScoped<RankEvaluationSweepJob>();
builder.Services.AddScoped<ProcessRankQueueJob>();
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

// AWS S3
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var accessKey = builder.Configuration["AWS:Credentials:AccessKey"];
    var secretKey = builder.Configuration["AWS:Credentials:SecretKey"];
    var region = builder.Configuration["AWS:S3:Region"] ?? "us-east-1";

    // When running on EC2/ECS with an IAM role, omit credentials and let the SDK use the instance profile.
    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        return new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));

    return new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
});
builder.Services.AddScoped<IS3FileService, S3FileService>();

// Certificate generation — fills PDF AcroForm templates stored in CertificateTemplates/
builder.Services.AddScoped<ICertificatePdfFillerService, ITextCertificatePdfFillerService>();

// Controllers
builder.Services.AddControllers();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror RankEngine API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
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

// Apply pending EF migrations automatically on startup (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseHangfireDashboard("/hangfire");
// Near-real-time: process RankEvaluationQueue entries written by SignupAPI
RecurringJob.AddOrUpdate<ProcessRankQueueJob>(
    "process-rank-queue",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// Nightly safety net: Phase 1 = recover missed queue entries, Phase 2 = full ambassador sweep
RecurringJob.AddOrUpdate<RankEvaluationSweepJob>(
    "rank-evaluation-sweep",
    job => job.ExecuteAsync(CancellationToken.None),
    "30 3 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.RankEngine",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

app.Run();
