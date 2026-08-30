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

// AQUÍ NO HAY AUTENTICACIÓN, Y ES A PROPÓSITO.
//
// La había —AddAuthentication + AddJwtBearer + AddAuthorization— y no protegía nada:
//
//   • CERO PANTALLAS Y CERO ENDPOINTS PROTEGIDOS. Esta aplicación es el asistente de alta: se
//     visita SIN sesión por definición, ninguna página lleva [Authorize], no hay
//     RequireAuthorization() en ninguna ruta y no se usa AuthorizeView ni el estado de
//     autenticación en ningún componente. De hecho lo que hace al cargar es lo contrario: rebotar
//     el navegador a los dos portales para MATAR cualquier sesión abierta (UsePortalSessionBounce).
//
//   • LA LLAVE ERA HMAC MIENTRAS SIGNUPAPI FIRMA CON RSA. El comentario decía "validates tokens
//     issued by SignupAPI" y era falso: ningún token emitido por SignupAPI habría validado nunca
//     contra una SymmetricSecurityKey.
//
//   • Y EL SECRETO ESTABA EN EL REPOSITORIO EN CLARO, en appsettings.json, con pinta de ser la
//     llave de firma de la casa. No lo era —no firma nada— pero cualquiera que lo leyera tenía
//     motivos para creerlo.
//
// Si algún día esta aplicación necesita distinguir a un visitante autenticado, lo que hay que
// traer es el bloque RSA de SignupAPI (Jwt:PublicKeyBase64, emisor y audiencia del sistema y el
// evento que rechaza los retos de 2FA), no volver a esto.

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
// Sin UseAuthentication/UseAuthorization: ver el bloque de arriba.
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MLMConquerorGlobalEdition.Signups.Client._Imports).Assembly);

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
