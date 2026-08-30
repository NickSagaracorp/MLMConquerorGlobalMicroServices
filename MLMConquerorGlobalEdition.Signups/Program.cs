using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.Signups.Components;
using MLMConquerorGlobalEdition.Signups.Middleware;
using MLMConquerorGlobalEdition.Signups.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor — Auto render mode (starts as Server, transitions to WASM after download)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// LA TOKENIZACIÓN DE TARJETA NO SE HACE AQUÍ. El asistente se prepinta en el servidor y por eso el
// contenedor tiene que poder construirlo, pero lo que se registra es un guardián que lanza si
// alguien intenta tokenizar de este lado. Quien tokeniza de verdad es el contenedor de WebAssembly
// (Signups.Client/Program.cs), en el navegador, donde está el número de tarjeta y donde se queda.
builder.Services.AddScoped<ICardTokenizationService, ServerSideCardTokenizationGuard>();

// HTTP client to call SignupAPI
builder.Services.AddHttpClient("SignupsInternal", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["SignupApiBaseUrl"] ?? "https://localhost:7148");
});

// JWT Authentication — validates tokens issued by SignupAPI
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// EL REBOTE. En un evento se dan de alta varias personas seguidas en el mismo ordenador y la
// anterior no siempre se acuerda de salir. Cargar una pantalla de alta manda el navegador, una sola
// vez por portal, al cierre de sesión de cada uno y lo devuelve aquí con el patrocinador intacto.
// Los portales y sus direcciones salen de configuración: cambian de un entorno a otro.
builder.Services.AddPortalSessionBounce(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();
// DELANTE DE TODO LO QUE PINTE, y esa es la mitad de la decisión: el navegador tiene que irse y
// volver ANTES de que el visitante vea un formulario. Desde la página —o desde cualquier sitio
// posterior— se iría a mitad de rellenarlo y volvería con todo en blanco. Va después de los
// estáticos solo por no hacerles mirar nada: el filtro de rutas ya los dejaría pasar.
app.UsePortalSessionBounce();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.Signups.Client._Imports).Assembly);

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
