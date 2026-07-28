namespace Blex.Generators;

internal sealed record StateModelBlex(string FieldName, string PropertyName, string TypeFqn) : System.IEquatable<StateModelBlex>;
