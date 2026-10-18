using System;
using System.Net.Http;
using Duack;
using Duack.KNI;
using Duack.Sample.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<global::Duack.Sample.Web.Pages.Index>("#app");

// KNI platform services
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Game factory for the KNI game loop
builder.Services.AddSingleton<Func<Game>>(sp => () => new Game1());

var host = builder.Build();
await host.RunAsync();
