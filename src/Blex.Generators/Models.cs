namespace Blex.Generators;

internal sealed record StateModel(string FieldName, string PropertyName, string TypeFqn) : System.IEquatable<StateModel>;

internal sealed record ComputedModel(string MethodName, string PropertyName, string TypeFqn) : System.IEquatable<ComputedModel>;

/// <param name="DefaultLiteral">C# literal for the parameter's default value, or null when none.</param>
/// <param name="IsParams">Whether this is a <c>params</c> array parameter.</param>
internal sealed record ParamModel(string TypeFqn, string Name, string? DefaultLiteral = null, bool IsParams = false) : System.IEquatable<ParamModel>;

internal sealed record ActionModel(
    string ImplName,
    string WrapperName,
    string ActionLabel,
    bool IsAsync,
    bool IsValueTask,
    EquatableArray<ParamModel> Parameters) : System.IEquatable<ActionModel>;

/// <param name="Concurrency">0 = Parallel, 1 = Latest, 2 = Drop, 3 = Queue (mirrors Blex.EffectConcurrency).</param>
/// <param name="HasCancellationToken">True when the impl method's last parameter is a CancellationToken supplied by the wrapper.</param>
internal sealed record EffectModel(
    string ImplName,
    string WrapperName,
    string ActionLabel,
    bool IsValueTask,
    int Concurrency,
    bool HasCancellationToken,
    EquatableArray<ParamModel> Parameters) : System.IEquatable<EffectModel>;

internal sealed record StoreModel(
    string? Namespace,
    string ClassName,
    string StoreName,
    bool Persist,
    EquatableArray<StateModel> States,
    EquatableArray<ComputedModel> Computeds,
    EquatableArray<ActionModel> Actions,
    EquatableArray<EffectModel> Effects) : System.IEquatable<StoreModel>;

/// <summary>
/// Location captured as plain values so pipeline outputs stay equatable (a raw
/// <see cref="Microsoft.CodeAnalysis.Location"/> roots syntax trees and defeats incremental caching).
/// </summary>
internal sealed record LocationInfo(string FilePath, int SpanStart, int SpanLength, int StartLine, int StartCharacter, int EndLine, int EndCharacter)
    : System.IEquatable<LocationInfo>
{
    public static LocationInfo? From(Microsoft.CodeAnalysis.Location? location)
    {
        if (location is null || location.SourceTree is null)
            return null;

        var span = location.GetLineSpan();
        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character);
    }

    public Microsoft.CodeAnalysis.Location ToLocation()
        => Microsoft.CodeAnalysis.Location.Create(
            FilePath,
            new Microsoft.CodeAnalysis.Text.TextSpan(SpanStart, SpanLength),
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                new Microsoft.CodeAnalysis.Text.LinePosition(StartLine, StartCharacter),
                new Microsoft.CodeAnalysis.Text.LinePosition(EndLine, EndCharacter)));
}

/// <summary>An equatable, cache-friendly stand-in for a <see cref="Microsoft.CodeAnalysis.Diagnostic"/>.</summary>
internal sealed record DiagnosticInfo(
    Microsoft.CodeAnalysis.DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> Args) : System.IEquatable<DiagnosticInfo>
{
    public Microsoft.CodeAnalysis.Diagnostic ToDiagnostic()
        => Microsoft.CodeAnalysis.Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            // ReSharper disable once CoVariantArrayConversion
            System.Linq.Enumerable.ToArray(Args));
}

/// <summary>The equatable output of the transform stage: an optional model plus diagnostics.</summary>
internal sealed record TransformOutput(
    StoreModel? Model,
    EquatableArray<DiagnosticInfo> Diagnostics) : System.IEquatable<TransformOutput>;
