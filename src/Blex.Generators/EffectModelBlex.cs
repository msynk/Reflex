namespace Blex.Generators;

/// <param name="Concurrency">0 = Parallel, 1 = Latest, 2 = Drop, 3 = Queue (mirrors Blex.EffectConcurrencyBlex).</param>
/// <param name="HasCancellationToken">True when the impl method's last parameter is a CancellationToken supplied by the wrapper.</param>
internal sealed record EffectModelBlex(
    string ImplName,
    string WrapperName,
    string ActionLabel,
    bool IsValueTask,
    int Concurrency,
    bool HasCancellationToken,
    EquatableArrayBlex<ParamModelBlex> Parameters) : System.IEquatable<EffectModelBlex>;
