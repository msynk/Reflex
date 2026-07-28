using Xunit;

namespace Blex.Generators.Tests;

public class DiagnosticTests
{
    private const string Usings = "using System.Threading; using System.Threading.Tasks; using Blex;\n";

    [Fact]
    public void NotPartial_ReportsBLEX001()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public class S { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("BLEX001"));
    }

    [Fact]
    public void ActionWithoutOnPrefix_ReportsBLEX002()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private void Increment() { } }
            """);
        Assert.True(result.HasDiagnostic("BLEX002"));
    }

    [Fact]
    public void ComputedWithBadName_ReportsBLEX003()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Computed] private int Twice() => 2; }
            """);
        Assert.True(result.HasDiagnostic("BLEX003"));
    }

    [Fact]
    public void ComputedWithParameters_ReportsBLEX004()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Computed] private int ComputeX(int y) => y; }
            """);
        Assert.True(result.HasDiagnostic("BLEX004"));
    }

    [Fact]
    public void CollidingGeneratedNames_ReportsBLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [State] private int _value;
                [Computed] private int ComputeValue() => 1;
            }
            """);
        Assert.True(result.HasDiagnostic("BLEX005"));
    }

    [Fact]
    public void CollisionWithUserMember_ReportsBLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [State] private int _count;
                public int Count => 42; // user already declared what the generator would emit
            }
            """);
        Assert.True(result.HasDiagnostic("BLEX005"));
    }

    [Fact]
    public void CollisionWithReservedName_ReportsBLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private string _name; }
            """);
        Assert.True(result.HasDiagnostic("BLEX005")); // Name is a StoreBase member
    }

    [Fact]
    public void NestedStore_ReportsBLEX006()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            public class Outer { [Store] public partial class S { [State] private int _x; } }
            """);
        Assert.True(result.HasDiagnostic("BLEX006"));
    }

    [Fact]
    public void SyncEffect_ReportsBLEX007()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Effect] private void OnLoad() { } }
            """);
        Assert.True(result.HasDiagnostic("BLEX007"));
    }

    [Fact]
    public void StaticStateField_ReportsBLEX008()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private static int _x; }
            """);
        Assert.True(result.HasDiagnostic("BLEX008"));
    }

    [Fact]
    public void ReadonlyStateField_ReportsBLEX008()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private readonly int _x; }
            """);
        Assert.True(result.HasDiagnostic("BLEX008"));
    }

    [Fact]
    public void LatestEffectWithoutToken_ReportsBLEX009Warning()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [Effect(Concurrency = EffectConcurrency.Latest)]
                private async Task OnSearch(string q) => await Task.Yield();
            }
            """);
        Assert.True(result.HasDiagnostic("BLEX009"));
        Assert.False(result.HasGeneratorError); // warning only; wrapper still generated
    }

    [Fact]
    public void AsyncVoidAction_ReportsBLEX010()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private async void OnFire() => await Task.Yield(); }
            """);
        Assert.True(result.HasDiagnostic("BLEX010"));
    }

    [Fact]
    public void ValueReturningAction_ReportsBLEX011Warning()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private int OnCalc() => 42; }
            """);
        Assert.True(result.HasDiagnostic("BLEX011"));
        Assert.False(result.HasGeneratorError);
    }

    [Fact]
    public void StateFieldWithoutPrefix_ReportsBLEX012()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private int Count; }
            """);
        Assert.True(result.HasDiagnostic("BLEX012"));
    }

    [Fact]
    public void RefParameter_ReportsBLEX013()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private void OnPush(ref int x) { } }
            """);
        Assert.True(result.HasDiagnostic("BLEX013"));
    }

    [Fact]
    public void GenericActionMethod_ReportsBLEX014()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private void OnSet<T>(T value) { } }
            """);
        Assert.True(result.HasDiagnostic("BLEX014"));
    }

    [Fact]
    public void RecordStore_ReportsBLEX015()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial record S { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("BLEX015"));
    }

    [Fact]
    public void StoreWithBaseClass_ReportsBLEX016()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            public class MyBase { }
            [Store] public partial class S : MyBase { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("BLEX016"));
    }

    [Fact]
    public void GenericStore_ReportsBLEX006()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S<T> { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("BLEX006"));
    }

    [Fact]
    public void StaticStore_ReportsBLEX006()
    {
        // A static class cannot derive from StoreBase or hold the instance members the generator
        // emits; without this the user gets four raw CS errors inside generated code.
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public static partial class S { }
            """);
        Assert.True(result.HasDiagnostic("BLEX006"));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void VoidComputed_ReportsBLEX017()
    {
        // `public void X { get { ... } }` does not compile; the memoized property needs a value.
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [State] private int _x;
                [Computed] private void ComputeThing() { }
            }
            """);
        Assert.True(result.HasDiagnostic("BLEX017"));
    }

    [Fact]
    public void VoidComputed_StillEmitsTheRestOfTheStoreWithoutCompileErrors()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [State] private int _x;
                [Computed] private void ComputeThing() { }
                [Computed] private int ComputeDouble() => X * 2;
                [Action] private void OnBump() => X++;
            }
            """);

        // The offending member is skipped rather than poisoning the whole emission, so the only
        // failure the user sees is the precise BLEX017 diagnostic.
        Assert.True(result.HasDiagnostic("BLEX017"));
        Assert.Empty(result.CompileErrors());
        Assert.Contains("public int Double", result.SingleGenerated);
    }
}
