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
using MLMConquerorGlobalEdition.SharedKernel.Server.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using CacheService = MLMConquerorGlobalEdition.SharedKernel.Server.Services.CacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using MLMConquerorGlobalEdition.RankEngine.Features.GetRankProgress;
using MLMConquerorGlobalEdition.RankEngine.Jobs;
using MLMConquerorGlobalEdition.RankEngine.Mappings;
using MLMConquerorGlobalEdition.RankEngine.Middleware;
using MLMConquerorGlobalEdition.RankEngine.Services;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services.Ranks;
using MLMConquerorGlobalEdition.Notifications;

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
// Rank services — single source of truth for ET points, PCP, and qualification.
builder.Services.AddRankServices();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();

builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

// Email/SMS transport — provider selected by Notifications:Email:Provider /
// Notifications:Sms:Provider config. Defaults to Null (log-only) when unset.
builder.Services.AddNotifications(builder.Configuration);

builder.Services.AddScoped<RankEvaluationSweepJob>();
builder.Services.AddScoped<ProcessRankQueueJob>();
// Fan-out notification jobs enqueued by EvaluateRankHandler — each runs on its own
// DI scope so concurrent rank evaluations never share an AppDbContext.
builder.Services.AddScoped<RankNotificationJobs>();
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("HangFire")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")));
// Restrict this Hangfire server to its own queue so it does not pick up
// jobs whose types live in assemblies this service does not reference.
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue("Hangfire:WorkerCount", 5);
    options.Queues = new[] { "rank" };
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

// Certificate generation — draws the recipient name + date onto the PDF templates
// stored in CertificateTemplates/ (resolved relative to the app content root).
builder.Services.AddScoped<ICertificatePdfFillerService>(sp =>
{
    var env    = sp.GetRequiredService<IWebHostEnvironment>();
    var logger = sp.GetRequiredService<ILogger<ITextCertificatePdfFillerService>>();
    var folder = Path.Combine(env.ContentRootPath, "CertificateTemplates");
    return new ITextCertificatePdfFillerService(folder, logger);
});

// Certificate storage — local filesystem until S3 credentials are available.
builder.Services.AddScoped<ICertificateStorage>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var cfg = sp.GetRequiredService<IConfiguration>();

    var provider = cfg["CertificateStorage:Provider"] ?? "Local";
    if (!string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        throw new NotSupportedException(
            $"CertificateStorage provider '{provider}' is not yet implemented. Use 'Local'.");

    var localPath = cfg["CertificateStorage:LocalPath"] ?? "wwwroot/certificates";
    var folder    = Path.IsPathRooted(localPath)
        ? localPath
        : Path.Combine(env.ContentRootPath, localPath);
    var baseUrl   = cfg["CertificateStorage:PublicBaseUrl"] ?? "https://localhost:7009";

    return new LocalCertificateStorage(folder, baseUrl);
});

// Controllers
builder.Services.AddControllers();

// JWT Authentication — matches AdminAPI/SignupAPI (RSA, asymmetric). Tokens are signed
// by AuthController with the PrivateKey; every API validates them with the PublicKey.
var publicKeyBase64 = JwtKeyGuard.ValidatePublicKey(builder.Configuration["Jwt:PublicKeyBase64"]);

var rsa = System.Security.Cryptography.RSA.Create();
rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
var jwtValidationKey = new RsaSecurityKey(rsa);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.UseSecurityTokenValidators = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
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

// Apply pending EF migrations and seed baseline data on startup (idempotent).
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await CompanyInfoSeeder.SeedAsync(db, logger);
    await RankGateSeeder.SeedAsync(db, logger);
}

app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();

// Serve generated certificate PDFs from wwwroot/certificates (unguessable file names).
app.UseStaticFiles();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseHangfireDashboard("/hangfire");
// Near-real-time: process RankEvaluationQueue entries written by SignupAPI.
// Queue MUST be "rank" — this RankEngine Hangfire server only processes that queue
// (per the per-service-queue isolation rule). Leaving it as "default" parks the job
// in a queue no server picks up, and after 5 retries Hangfire marks the recurring job
// as poisoned and stops scheduling it.
RecurringJob.AddOrUpdate<ProcessRankQueueJob>(
    "process-rank-queue",
    "rank",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

// Nightly safety net: Phase 1 = recover missed queue entries, Phase 2 = full ambassador sweep
RecurringJob.AddOrUpdate<RankEvaluationSweepJob>(
    "rank-evaluation-sweep",
    "rank",
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
