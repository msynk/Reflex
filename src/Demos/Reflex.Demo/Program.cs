using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Reflex;
using Reflex.Blazor;
using Reflex.Demo;
using Reflex.Demo.Services;
using Reflex.Demo.Stores;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- Site services (not part of Reflex; they power the live panels) --------------------------
builder.Services.AddScoped<ActionFeed>();
builder.Services.AddScoped<DemoGuard>();
builder.Services.AddScoped<ThemeService>();

// OnError is captured on the singleton options object, so the sink it writes to must outlive
// any single scope. In a Blazor Server app you would forward to ILogger instead.
var errorLog = new ErrorLog();
builder.Services.AddSingleton(errorLog);

// --- Reflex ----------------------------------------------------------------------------------
builder.Services.AddReflex(options =>
{
    options.DevToolsName = "Reflex Docs";

    // Observing middleware, resolved from DI so it can take scoped dependencies.
    options.UseMiddleware<ActionFeedMiddleware>();

    // Veto middleware: cancels actions while the demo's read-only guard is on.
    options.UseMiddleware<GuardMiddleware>();

    // Display-only redaction: the settings store's Token never reaches the DevTools monitor.
    options.RedactDevToolsKeys("Token");

    // Every non-fatal failure Reflex isolates from the pipeline lands here.
    options.OnError = errorLog.Add;
});

builder.Services.AddReflexStore<CounterStore>();
builder.Services.AddReflexStore<CartStore>();
builder.Services.AddReflexStore<EqualityDemoStore>();
builder.Services.AddReflexStore<EffectLabStore>();
builder.Services.AddReflexStore<SearchStore>();
builder.Services.AddReflexStore<ContactsStore>();
builder.Services.AddReflexStore<SettingsStore>();
builder.Services.AddReflexStore<OrdersStore>();
builder.Services.AddReflexStore<NotificationsStore>();

// Durable persistence for [Store(Persist = true)] stores, with debounced writes and a
// versioned envelope so the shape can evolve without stranding visitors on old payloads.
builder.Services.AddReflexLocalStoragePersistence(options =>
{
    options.KeyPrefix = "reflex-docs:";
    options.DebounceInterval = TimeSpan.FromMilliseconds(300);
    options.DebounceMaxDelay = TimeSpan.FromSeconds(2);
    options.Version = 1;
    options.Migrate = (storeName, fromVersion, state) =>
    {
        // v0 payloads predate the versioned envelope; take them as-is.
        if (fromVersion == 0)
            return state;

        // Anything else is from a future build of the site: discard rather than guess.
        return null;
    };
});

// In-app undo/redo across every store. <ReflexProvider> calls Start() once rehydration finishes.
builder.Services.AddReflexHistory(maxEntries: 50);

await builder.Build().RunAsync();
