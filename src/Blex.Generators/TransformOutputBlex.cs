namespace Blex.Generators;

/// <summary>The equatable output of the transform stage: an optional model plus diagnostics.</summary>
internal sealed record TransformOutputBlex(
    StoreModelBlex? Model,
    EquatableArrayBlex<DiagnosticInfoBlex> Diagnostics) : System.IEquatable<TransformOutputBlex>;
