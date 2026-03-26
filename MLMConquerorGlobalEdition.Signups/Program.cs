using System.Text;
using Amazon.S3;
using AspNetCoreRateLimit;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Seeders;
using MLMConquerorGlobalEdition.Repository.Services;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using IPushNotificationService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IPushNotificationService;
using MLMConquerorGlobalEdition.Signups.Components;
using MLMConquerorGlobalEdition.Signups.Jobs;
using MLMConquerorGlobalEdition.Signups.Mappings;
using MLMConquerorGlobalEdition.Signups.Middleware;
using MLMConquerorGlobalEdition.Signups.Services;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Identity — use AddIdentityCore to avoid overriding JWT as the default auth scheme
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

// MediatR — scans all handlers in this assembly + error-handling pipeline behavior
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(SignupsMappingProfile));

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

// AWS S3
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var accessKey = builder.Configuration["AWS:Credentials:AccessKey"];
    var secretKey = builder.Configuration["AWS:Credentials:SecretKey"];
    var region = builder.Configuration["AWS:S3:Region"] ?? "us-east-1";

    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        return new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));

    return new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
});
builder.Services.AddScoped<IS3FileService, S3FileService>();
builder.Services.AddScoped<ISponsorBonusService, SponsorBonusService>();

// JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Error Tracking — singleton; uses IServiceScopeFactory for isolated DB writes
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();

// ── Firebase push notifications ───────────────────────────────────────────────
builder.Services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

// ── HangFire ──────────────────────────────────────────────────────────────────
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

// Controllers
builder.Services.AddControllers();

// Blazor Server — signup wizard UI served at /signup/{slug}
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Self-referencing HTTP client for Blazor components to call the Signups API
builder.Services.AddHttpClient("SignupsInternal", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["SignupsApiBaseUrl"] ?? "https://localhost:7147");
});

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
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror Signups API", Version = "v1" });
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

// Seed roles and root ambassador
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
}

app.UseStaticFiles();          // must be before auth so CSS/JS are public
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

// Blazor Server — serves the signup wizard at /signup and /signup/{slug}
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── HangFire dashboard & recurring jobs ───────────────────────────────────────
app.UseHangfireDashboard("/hangfire");
RecurringJob.AddOrUpdate<ProcessScheduledCancellationsJob>(
    "process-scheduled-cancellations",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 1 * * *",
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
