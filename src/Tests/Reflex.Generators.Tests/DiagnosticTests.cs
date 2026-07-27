using Xunit;

namespace Reflex.Generators.Tests;

public class DiagnosticTests
{
    private const string Usings = "using System.Threading; using System.Threading.Tasks; using Reflex;\n";

    [Fact]
    public void NotPartial_ReportsREFLEX001()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public class S { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX001"));
    }

    [Fact]
    public void ActionWithoutOnPrefix_ReportsREFLEX002()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private void Increment() { } }
            """);
        Assert.True(result.HasDiagnostic("REFLEX002"));
    }

    [Fact]
    public void ComputedWithBadName_ReportsREFLEX003()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Computed] private int Twice() => 2; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX003"));
    }

    [Fact]
    public void ComputedWithParameters_ReportsREFLEX004()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Computed] private int ComputeX(int y) => y; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX004"));
    }

    [Fact]
    public void CollidingGeneratedNames_ReportsREFLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [State] private int _value;
                [Computed] private int ComputeValue() => 1;
            }
            """);
        Assert.True(result.HasDiagnostic("REFLEX005"));
    }

    [Fact]
    public void CollisionWithUserMember_ReportsREFLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [State] private int _count;
                public int Count => 42; // user already declared what the generator would emit
            }
            """);
        Assert.True(result.HasDiagnostic("REFLEX005"));
    }

    [Fact]
    public void CollisionWithReservedName_ReportsREFLEX005()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private string _name; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX005")); // Name is a StoreBase member
    }

    [Fact]
    public void NestedStore_ReportsREFLEX006()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            public class Outer { [Store] public partial class S { [State] private int _x; } }
            """);
        Assert.True(result.HasDiagnostic("REFLEX006"));
    }

    [Fact]
    public void SyncEffect_ReportsREFLEX007()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Effect] private void OnLoad() { } }
            """);
        Assert.True(result.HasDiagnostic("REFLEX007"));
    }

    [Fact]
    public void StaticStateField_ReportsREFLEX008()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private static int _x; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX008"));
    }

    [Fact]
    public void ReadonlyStateField_ReportsREFLEX008()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private readonly int _x; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX008"));
    }

    [Fact]
    public void LatestEffectWithoutToken_ReportsREFLEX009Warning()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S
            {
                [Effect(Concurrency = EffectConcurrency.Latest)]
                private async Task OnSearch(string q) => await Task.Yield();
            }
            """);
        Assert.True(result.HasDiagnostic("REFLEX009"));
        Assert.False(result.HasGeneratorError); // warning only; wrapper still generated
    }

    [Fact]
    public void AsyncVoidAction_ReportsREFLEX010()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private async void OnFire() => await Task.Yield(); }
            """);
        Assert.True(result.HasDiagnostic("REFLEX010"));
    }

    [Fact]
    public void ValueReturningAction_ReportsREFLEX011Warning()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private int OnCalc() => 42; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX011"));
        Assert.False(result.HasGeneratorError);
    }

    [Fact]
    public void StateFieldWithoutPrefix_ReportsREFLEX012()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [State] private int Count; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX012"));
    }

    [Fact]
    public void RefParameter_ReportsREFLEX013()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private void OnPush(ref int x) { } }
            """);
        Assert.True(result.HasDiagnostic("REFLEX013"));
    }

    [Fact]
    public void GenericActionMethod_ReportsREFLEX014()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S { [Action] private void OnSet<T>(T value) { } }
            """);
        Assert.True(result.HasDiagnostic("REFLEX014"));
    }

    [Fact]
    public void RecordStore_ReportsREFLEX015()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial record S { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX015"));
    }

    [Fact]
    public void StoreWithBaseClass_ReportsREFLEX016()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            public class MyBase { }
            [Store] public partial class S : MyBase { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX016"));
    }

    [Fact]
    public void GenericStore_ReportsREFLEX006()
    {
        var result = GeneratorTestHelper.Run(Usings + """
            [Store] public partial class S<T> { [State] private int _x; }
            """);
        Assert.True(result.HasDiagnostic("REFLEX006"));
    }
}
