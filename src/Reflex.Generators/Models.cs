namespace Reflex.Generators;

internal sealed record StateModel(string FieldName, string PropertyName, string TypeFqn) : System.IEquatable<StateModel>;

internal sealed record ComputedModel(string MethodName, string PropertyName, string TypeFqn) : System.IEquatable<ComputedModel>;

internal sealed record ParamModel(string TypeFqn, string Name) : System.IEquatable<ParamModel>;

internal sealed record ActionModel(
    string ImplName,
    string WrapperName,
    string ActionLabel,
    bool IsAsync,
    bool IsValueTask,
    EquatableArray<ParamModel> Parameters) : System.IEquatable<ActionModel>;

internal sealed record EffectModel(
    string ImplName,
    string WrapperName,
    string ActionLabel,
    bool IsValueTask,
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
