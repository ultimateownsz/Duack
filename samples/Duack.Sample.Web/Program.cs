using System.Net.Http;
using Duack;
using Duack.KNI;
using Duack.Sample.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<global::Duack.Sample.Web.Index>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Wire the browser settings store (the Duack.KNI integration point): plain C# logic over the
// synchronous localStorage interop seam, selected for the browser platform before the app boots.
builder.Services.AddScoped<IKeyValueStorage, LocalStorageKeyValueStorage>();
builder.Services.AddScoped<IPlayerSettingsStore>(sp =>
    new WebPlayerSettingsStore(sp.GetRequiredService<IKeyValueStorage>()));

var host = builder.Build();
PlayerSettings.Store = PlayerSettingsStoreSelector.Select(
    isBrowser: true,
    fileStore: new FilePlayerSettingsStore(),
    browserStore: host.Services.GetRequiredService<IPlayerSettingsStore>());

await host.RunAsync();
