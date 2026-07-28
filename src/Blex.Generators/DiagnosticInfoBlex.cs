namespace Blex.Generators;

/// <summary>An equatable, cache-friendly stand-in for a <see cref="Microsoft.CodeAnalysis.Diagnostic"/>.</summary>
internal sealed record DiagnosticInfoBlex(
    Microsoft.CodeAnalysis.DiagnosticDescriptor Descriptor,
    LocationInfoBlex? Location,
    EquatableArrayBlex<string> Args) : System.IEquatable<DiagnosticInfoBlex>
{
    public Microsoft.CodeAnalysis.Diagnostic ToDiagnostic()
        => Microsoft.CodeAnalysis.Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            // ReSharper disable once CoVariantArrayConversion
            System.Linq.Enumerable.ToArray(Args));
}
