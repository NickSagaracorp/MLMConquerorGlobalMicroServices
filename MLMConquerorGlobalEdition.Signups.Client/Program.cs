using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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

await builder.Build().RunAsync();
