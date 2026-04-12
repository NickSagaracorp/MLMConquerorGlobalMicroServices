using System.Security.Cryptography;
using AspNetCoreRateLimit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLMConquerorGlobalEdition.AdminAPI.Middleware;
using MLMConquerorGlobalEdition.AdminAPI.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.Repository.Services;
using FluentValidation;
using MLMConquerorGlobalEdition.SharedKernel.Behaviors;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SharedKernel.Logging;
using MLMConquerorGlobalEdition.Repository.Seeders;
using ICacheService         = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;
using IEmailService         = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IEmailService;
using IErrorTrackingService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IErrorTrackingService;
using ICurrentUserService   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService;
using IDateTimeProvider     = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;
using CacheService          = MLMConquerorGlobalEdition.SharedKernel.Services.CacheService;
using NullEmailService      = MLMConquerorGlobalEdition.SharedKernel.Services.NullEmailService;
using JwtService            = MLMConquerorGlobalEdition.AdminAPI.Services.JwtService;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPiiMaskingConsole();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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
.AddDefaultTokenProviders();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();


builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddSingleton<IHtmlSanitizerService, HtmlSanitizerService>();
builder.Services.AddSingleton<IErrorTrackingService, ErrorTrackingService>();
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
});
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddTransient<IEmailService, NullEmailService>();

builder.Services.AddControllers();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? (builder.Environment.IsDevelopment()
        ? new[] { "https://localhost:7001", "https://localhost:7003" }
        : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminApiPolicy", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
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

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("HealthCheck")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLMConqueror AdminAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
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
}

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<DomainExceptionMiddleware>();
app.UseCors("AdminApiPolicy");

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();

app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    var status = canConnect ? "Healthy" : "Unhealthy";
    return Results.Ok(new
    {
        service   = "MLMConquerorGlobalEdition.AdminAPI",
        status,
        checks    = new { database = canConnect ? "Healthy" : "Unhealthy" },
        timestamp = DateTime.UtcNow
    });
}).AllowAnonymous();

app.Run();
