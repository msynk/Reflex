namespace Blex.Generators;

internal sealed record ActionModelBlex(
    string ImplName,
    string WrapperName,
    string ActionLabel,
    bool IsAsync,
    bool IsValueTask,
    EquatableArrayBlex<ParamModelBlex> Parameters) : System.IEquatable<ActionModelBlex>;
