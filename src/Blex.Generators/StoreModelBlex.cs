namespace Blex.Generators;

internal sealed record StoreModelBlex(
    string? Namespace,
    string ClassName,
    string StoreName,
    bool Persist,
    EquatableArrayBlex<StateModelBlex> States,
    EquatableArrayBlex<ComputedModelBlex> Computeds,
    EquatableArrayBlex<ActionModelBlex> Actions,
    EquatableArrayBlex<EffectModelBlex> Effects) : System.IEquatable<StoreModelBlex>;
