using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MLMConquerorGlobalEdition.SharedKernel.Billing;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Named client used by Signup.razor and MemberJoin.razor via IHttpClientFactory.
// In WASM the browser calls SignupAPI directly at port 7005.
builder.Services.AddHttpClient("SignupsInternal", client =>
{
    client.BaseAddress = new Uri("https://localhost:7005");
});

// Default client kept for any component that injects HttpClient directly.
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// ── TOKENIZACIÓN DE TARJETA ────────────────────────────────────────────────────────────────────
// ESTE REGISTRO ES EL QUE MANTIENE EL NÚMERO DE TARJETA DENTRO DEL NAVEGADOR.
//
// Este contenedor es el de WebAssembly: lo que se resuelva aquí se ejecuta en el dispositivo de la
// persona, no en nuestros servidores. El asistente teclea el PAN, se lo da a este servicio y manda
// a la API únicamente los tres identificadores que salen de él. El número no cruza la red hacia
// nosotros y nuestra infraestructura no entra en el alcance de PCI DSS por esta vía.
//
// EN PRODUCCIÓN SE CAMBIA ESTA LÍNEA Y NADA MÁS. La implementación real —Stripe.js o el iframe de
// Spreedly, por JSInterop desde aquí mismo— cumple la misma interfaz y habla directamente con la
// pasarela desde el navegador. Ni Signup.razor ni MemberJoin.razor se enteran.
//
// LO QUE NO SE PUEDE HACER: registrar esto en el contenedor del SERVIDOR (Signups/Program.cs). Allí
// hay un guardián que lanza si alguien lo intenta, porque tokenizar en el servidor obliga a que el
// PAN viaje hasta él, que es exactamente lo que esta separación evita.
builder.Services.AddScoped<ICardTokenizationService, SimulatedCardTokenizationService>();

await builder.Build().RunAsync();
