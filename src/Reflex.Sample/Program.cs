using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Reflex;
using Reflex.Blazor;
using Reflex.Sample;
using Reflex.Sample.Stores;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register Reflex with a console-logging middleware, then add the stores.
builder.Services.AddReflex(options =>
{
    options.DevToolsName = "Reflex Sample";
    options.UseMiddleware(ctx =>
        Console.WriteLine($"[reflex] {ctx.QualifiedName} #{ctx.Sequence}"));
    // Demonstrate the DevTools state sanitizer (display-only redaction).
    options.RedactDevToolsKeys("secret");
});
builder.Services.AddReflexStore<CounterStore>();
builder.Services.AddReflexStore<TodoStore>();

// Persist [Store(Persist = true)] stores to localStorage, and enable in-app undo/redo.
builder.Services.AddReflexLocalStoragePersistence();
builder.Services.AddReflexHistory();

await builder.Build().RunAsync();
