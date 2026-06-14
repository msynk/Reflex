using System.Collections.Generic;
using System.Text.Json.Nodes;
using Reflex;
using Xunit;

namespace Reflex.Tests;

public class SanitizerTests
{
    private sealed class CapturingDevTools : IReflexDevTools
    {
        public List<JsonObject> Inits { get; } = new();
        public List<(string Action, JsonObject State)> Sends { get; } = new();
        public void Init(JsonObject globalState) => Inits.Add(globalState);
        public void Send(string actionName, JsonObject globalState) => Sends.Add((actionName, globalState));
    }

    [Fact]
    public void StateSanitizer_AppliesToSentState_NotInternalState()
    {
        var sink = new CapturingDevTools();
        var manager = new ReflexManager
        {
            DevToolsStateSanitizer = state =>
            {
                var clone = (JsonObject)state.DeepClone();
                clone["counter"]!["Label"] = "<redacted>";
                return clone;
            },
        };
        var counter = new CounterStore();
        manager.Register(counter);
        manager.ConnectDevTools(sink);

        counter.Increment();

        // Sent state is sanitized...
        Assert.Equal("<redacted>", sink.Sends[0].State["counter"]!["Label"]!.GetValue<string>());
        // ...but the actual store/global state is untouched.
        Assert.Equal("idle", manager.CaptureGlobalState()["counter"]!["Label"]!.GetValue<string>());
    }

    [Fact]
    public void ActionSanitizer_RewritesActionLabel()
    {
        var sink = new CapturingDevTools();
        var manager = new ReflexManager
        {
            DevToolsActionSanitizer = label => label.ToUpperInvariant(),
        };
        var counter = new CounterStore();
        manager.Register(counter);
        manager.ConnectDevTools(sink);

        counter.Increment();

        Assert.Equal("COUNTER/INCREMENT", sink.Sends[0].Action);
    }

    [Fact]
    public void RedactDevToolsKeys_RedactsMatchingKeysRecursively()
    {
        var options = new ReflexOptions();
        options.RedactDevToolsKeys("Label");

        var state = new JsonObject
        {
            ["counter"] = new JsonObject { ["Count"] = 1, ["Label"] = "secret" },
        };

        var sanitized = options.DevToolsStateSanitizer!(state);

        Assert.Equal("<redacted>", sanitized["counter"]!["Label"]!.GetValue<string>());
        Assert.Equal(1, sanitized["counter"]!["Count"]!.GetValue<int>());
        // Original untouched (sanitizer clones).
        Assert.Equal("secret", state["counter"]!["Label"]!.GetValue<string>());
    }

    [Fact]
    public void Sanitizer_Exception_FallsBackToOriginal()
    {
        var sink = new CapturingDevTools();
        var manager = new ReflexManager
        {
            DevToolsStateSanitizer = _ => throw new System.Exception("boom"),
        };
        var counter = new CounterStore();
        manager.Register(counter);
        manager.ConnectDevTools(sink);

        counter.Increment(); // must not throw

        Assert.Single(sink.Sends);
    }
}
