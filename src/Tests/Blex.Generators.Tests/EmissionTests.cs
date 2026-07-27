using Xunit;

namespace Reflex.Generators.Tests;

public class EmissionTests
{
    private const string Usings = "using System.Threading; using System.Threading.Tasks; using Reflex;\n";

    [Fact]
    public void HappyPath_CompilesWithoutErrors()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;

            [Store(Name = "counter", Persist = true)]
            public partial class CounterStore
            {
                [State] private int _count;
                [State] private string? _label;

                [Computed] private int ComputeDouble() => Count * 2;

                [Action] private void OnIncrement() => Count++;
                [Action] private void OnAdd(int amount, string? note = null) => Count += amount;

                [Effect(Concurrency = EffectConcurrency.Latest)]
                private async Task OnLoad(int id, CancellationToken ct) => await Task.Delay(1, ct);

                [Effect(Concurrency = EffectConcurrency.Queue)]
                private async Task OnSave() => await Task.Yield();
            }
            """);

        Assert.False(result.HasGeneratorError);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void NullableAnnotations_ArePreserved()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S { [State] private string? _userName; }
            """);

        Assert.Contains("public string? UserName", result.SingleGenerated);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void DefaultParameterValues_ArePropagatedToWrapper()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _x;
                [Action] private void OnSet(int value, bool log = true, string tag = "none") => X = value;
            }
            """);

        var generated = result.SingleGenerated;
        Assert.Contains("bool log = true", generated);
        Assert.Contains("string tag = \"none\"", generated);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void CancellationTokenParameter_IsHiddenFromPublicWrapper_AndCancelMethodEmitted()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [Effect] private async Task OnLoad(int id, CancellationToken ct) => await Task.Delay(1, ct);
            }
            """);

        var generated = result.SingleGenerated;
        Assert.Contains("public async global::System.Threading.Tasks.Task Load(int id)", generated);
        Assert.Contains("public void CancelLoad()", generated);
        Assert.DoesNotContain("Load(int id, ", generated);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void SameClassName_InDifferentNamespaces_GetsDistinctHintNames()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace A { [Store] public partial class CartStore { [State] private int _n; } }
            namespace B { [Store] public partial class CartStore { [State] private int _n; } }
            """);

        Assert.Equal(2, result.GeneratedSources.Count);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void ActionLabelWithQuotes_IsEscaped()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store(Name = "the \"weird\" store")]
            public partial class S
            {
                [State] private int _x;
                [Action(Name = "say \"hi\"")] private void OnGreet() => X++;
            }
            """);

        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void GlobalNamespaceStore_Compiles()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class GlobalStore { [State] private int _x; }
            """);

        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void JsonNullRestore_AssignsDefault()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S { [State] private string? _text; }
            """);

        // Present-but-null must restore to default!, only a missing property is skipped.
        Assert.Contains("is null ? default!", result.SingleGenerated);
    }

    [Fact]
    public void NullableEnumDefaultParameter_Compiles()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            public enum Mode { A = 0, B = 1 }
            [Store] public partial class S
            {
                [State] private int _x;
                [Action] private void OnSet(Mode? mode = Mode.B, int? count = 5, bool? flag = null) => X++;
            }
            """);

        Assert.False(result.HasGeneratorError);
        Assert.Empty(result.CompileErrors());
        Assert.Contains("(global::App.Mode)(1)", result.SingleGenerated);
    }

    [Fact]
    public void KeywordActionName_IsTreatedAsLabelOnly()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _x;
                [Action(Name = "lock")] private void OnLock() => X++;
            }
            """);

        // "lock" is a reserved keyword: it must become the display label, not the method name.
        Assert.Contains("public void Lock()", result.SingleGenerated);
        Assert.DoesNotContain("public void lock(", result.SingleGenerated);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void ControlCharactersInName_AreEscaped()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store(Name = "line\nbreak\ttab")]
            public partial class S
            {
                [State] private int _x;
                [Action(Name = "do\nthing")] private void OnGo() => X++;
            }
            """);

        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void DuplicateMembers_SuppressEmission_InsteadOfEmittingBrokenCode()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _value;
                [Computed] private int ComputeValue() => 1;
            }
            """);

        Assert.True(result.HasDiagnostic("REFLEX005"));
        // No generated source: emitting would just bury the user in CS0102 cascades.
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void UserFieldCollidingWithGeneratedBackingField_ReportsREFLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _x;
                [Computed] private int ComputeFoo() => X;
                private bool __FooValid; // collides with the memoization backing field
            }
            """);

        Assert.True(result.HasDiagnostic("REFLEX005"));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void KeywordParameterNames_AreEscaped()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _x;
                [Action] private void OnDoThing(int @lock, string @class) => X = @lock + @class.Length;
                [Effect] private async Task OnLoad(int @event, CancellationToken ct) { await Task.Delay(1, ct); X = @event; }
            }
            """);

        Assert.Contains("int @lock", result.SingleGenerated);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void FieldsNamedLikeGeneratedLocals_BindCorrectly()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private string? state;   // collides with DeserializeState's parameter
                [State] private int value;       // collides with the setter's implicit parameter
            }
            """);

        // this.-qualification must bind the assignments to the fields, not the parameters.
        Assert.Contains("this.state", result.SingleGenerated);
        Assert.Contains("this.value", result.SingleGenerated);
        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void UserParameterNamed__ct_DoesNotCollideWithWrapperLocals()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _x;
                [Effect(Concurrency = EffectConcurrency.Latest)]
                private async Task OnLoad(int __ct, CancellationToken ct) { await Task.Delay(1, ct); X = __ct; }
            }
            """);

        Assert.Empty(result.CompileErrors());
    }

    [Fact]
    public void ParamsParameter_IsPreserved()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            namespace App;
            [Store] public partial class S
            {
                [State] private int _total;
                [Action] private void OnAddAll(params int[] values) { foreach (var v in values) Total += v; }
            }
            """);

        Assert.Contains("params int[] values", result.SingleGenerated);
        Assert.Empty(result.CompileErrors());
    }
}
