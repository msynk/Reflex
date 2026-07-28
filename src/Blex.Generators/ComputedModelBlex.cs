namespace Blex.Generators;

internal sealed record ComputedModelBlex(string MethodName, string PropertyName, string TypeFqn) : System.IEquatable<ComputedModelBlex>;
