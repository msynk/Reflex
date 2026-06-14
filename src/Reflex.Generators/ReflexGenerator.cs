using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Reflex.Generators;

/// <summary>
/// Generates the reactive plumbing for classes annotated with <c>[Reflex.Store]</c>:
/// reactive properties, memoized computed accessors, named action wrappers, JSON snapshot
/// support and the <c>StoreBase</c> base type.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ReflexGenerator : IIncrementalGenerator
{
    private const string StoreAttribute = "Reflex.StoreAttribute";
    private const string StateAttribute = "Reflex.StateAttribute";
    private const string ComputedAttribute = "Reflex.ComputedAttribute";
    private const string ActionAttribute = "Reflex.ActionAttribute";

    private static readonly DiagnosticDescriptor NotPartial = new(
        "REFLEX001",
        "Store class must be partial",
        "Reflex store '{0}' must be declared 'partial' so the generator can extend it",
        "Reflex",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadActionName = new(
        "REFLEX002",
        "Cannot derive action name",
        "Action method '{0}' must start with 'On' or specify [Action(Name = \"...\")] to derive a public action name",
        "Reflex",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor BadComputedName = new(
        "REFLEX003",
        "Cannot derive computed name",
        "Computed method '{0}' must start with 'Compute' or 'Get' to derive a property name",
        "Reflex",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ComputedHasParameters = new(
        "REFLEX004",
        "Computed method must be parameterless",
        "Computed method '{0}' must be parameterless so it can back a memoized property",
        "Reflex",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateMember = new(
        "REFLEX005",
        "Generated member name collides",
        "Reflex store '{0}' generates more than one member named '{1}'. Rename the conflicting state, computed or action.",
        "Reflex",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedClassShape = new(
        "REFLEX006",
        "Unsupported store class shape",
        "Reflex store '{0}' must be a top-level, non-generic class. Nested and generic stores are not supported.",
        "Reflex",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var stores = context.SyntaxProvider.ForAttributeWithMetadataName(
            StoreAttribute,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => Transform(ctx))
            .Where(static x => x is not null);

        context.RegisterSourceOutput(stores, static (spc, result) =>
        {
            foreach (var diagnostic in result!.Diagnostics)
                spc.ReportDiagnostic(diagnostic);

            if (result.Model is { } model)
                spc.AddSource($"{model.ClassName}.Reflex.g.cs", SourceText.From(Emit(model), Encoding.UTF8));
        });
    }

    private sealed class TransformResult
    {
        public StoreModel? Model { get; set; }
        public List<Diagnostic> Diagnostics { get; } = [];
    }

    private static TransformResult Transform(GeneratorAttributeSyntaxContext ctx)
    {
        var result = new TransformResult();
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
        var classDecl = (ClassDeclarationSyntax)ctx.TargetNode;

        if (!classDecl.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
        {
            result.Diagnostics.Add(Diagnostic.Create(NotPartial, classDecl.Identifier.GetLocation(), symbol.Name));
            return result;
        }

        // The emitter writes a flat, top-level partial declaration, so nested and generic
        // stores would produce broken output. Fail with a clear diagnostic instead.
        if (symbol.ContainingType is not null || symbol.IsGenericType)
        {
            result.Diagnostics.Add(Diagnostic.Create(UnsupportedClassShape, classDecl.Identifier.GetLocation(), symbol.Name));
            return result;
        }

        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        var storeName = symbol.Name;
        var storeAttr = ctx.Attributes.FirstOrDefault();
        if (storeAttr is not null)
        {
            var named = storeAttr.NamedArguments.FirstOrDefault(a => a.Key == "Name").Value;
            if (named.Value is string s && !string.IsNullOrWhiteSpace(s))
                storeName = s;
        }

        var states = new List<StateModel>();
        var computeds = new List<ComputedModel>();
        var actions = new List<ActionModel>();

        foreach (var member in symbol.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol field when HasAttribute(field, StateAttribute):
                    states.Add(new StateModel(
                        field.Name,
                        ToPropertyName(field.Name),
                        field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    break;

                case IMethodSymbol cm when cm.MethodKind == MethodKind.Ordinary && HasAttribute(cm, ComputedAttribute):
                {
                    if (cm.Parameters.Length > 0)
                    {
                        result.Diagnostics.Add(Diagnostic.Create(ComputedHasParameters, cm.Locations.FirstOrDefault(), cm.Name));
                        break;
                    }

                    var propName = ToComputedName(cm.Name);
                    if (propName is null)
                    {
                        result.Diagnostics.Add(Diagnostic.Create(BadComputedName, cm.Locations.FirstOrDefault(), cm.Name));
                        break;
                    }

                    computeds.Add(new ComputedModel(
                        cm.Name,
                        propName,
                        cm.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    break;
                }

                case IMethodSymbol am when am.MethodKind == MethodKind.Ordinary && HasAttribute(am, ActionAttribute):
                {
                    var attr = am.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == ActionAttribute);
                    var explicitName = attr.NamedArguments
                        .FirstOrDefault(a => a.Key == "Name").Value.Value as string;

                    var wrapper = DeriveWrapperName(am.Name, explicitName);
                    if (wrapper is null)
                    {
                        result.Diagnostics.Add(Diagnostic.Create(BadActionName, am.Locations.FirstOrDefault(), am.Name));
                        break;
                    }

                    // The label is for display (DevTools / time-travel) and may contain spaces;
                    // the wrapper is a C# identifier and is kept separate.
                    var label = string.IsNullOrWhiteSpace(explicitName) ? wrapper : explicitName!;

                    var isAsync = IsTaskReturning(am.ReturnType);
                    var isValueTask = IsValueTaskReturning(am.ReturnType);
                    var prms = am.Parameters
                        .Select(p => new ParamModel(
                            p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            p.Name))
                        .ToArray();

                    actions.Add(new ActionModel(
                        am.Name,
                        wrapper,
                        label,
                        isAsync,
                        isValueTask,
                        new EquatableArray<ParamModel>(prms)));
                    break;
                }
            }
        }

        result.Model = new StoreModel(
            ns,
            symbol.Name,
            storeName,
            new EquatableArray<StateModel>(states.ToArray()),
            new EquatableArray<ComputedModel>(computeds.ToArray()),
            new EquatableArray<ActionModel>(actions.ToArray()));

        DetectDuplicates(storeName, states, computeds, actions, symbol, result);

        return result;
    }

    private static void DetectDuplicates(
        string storeName,
        List<StateModel> states,
        List<ComputedModel> computeds,
        List<ActionModel> actions,
        INamedTypeSymbol symbol,
        TransformResult result)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        void Check(string name)
        {
            if (!seen.Add(name))
                result.Diagnostics.Add(Diagnostic.Create(DuplicateMember, symbol.Locations.FirstOrDefault(), storeName, name));
        }

        foreach (var s in states)
            Check(s.PropertyName);
        foreach (var c in computeds)
            Check(c.PropertyName);
        foreach (var a in actions)
            Check(a.WrapperName);
    }

    private static bool HasAttribute(ISymbol symbol, string fullName)
        => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == fullName);

    private static bool IsTaskReturning(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        return name == "System.Threading.Tasks.Task"
            || name.StartsWith("System.Threading.Tasks.Task<", System.StringComparison.Ordinal)
            || name == "System.Threading.Tasks.ValueTask"
            || name.StartsWith("System.Threading.Tasks.ValueTask<", System.StringComparison.Ordinal);
    }

    private static bool IsValueTaskReturning(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        return name == "System.Threading.Tasks.ValueTask"
            || name.StartsWith("System.Threading.Tasks.ValueTask<", System.StringComparison.Ordinal);
    }

    private static string ToPropertyName(string fieldName)
    {
        var trimmed = fieldName.TrimStart('_');
        if (trimmed.Length == 0)
            trimmed = fieldName;
        if (trimmed.Length == 0 || !(char.IsLetter(trimmed[0]) || trimmed[0] == '_'))
            return "_" + trimmed;
        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    private static string? ToComputedName(string methodName)
    {
        string? core = null;
        if (methodName.StartsWith("Compute", System.StringComparison.Ordinal) && methodName.Length > 7)
            core = methodName.Substring(7);
        else if (methodName.StartsWith("Get", System.StringComparison.Ordinal) && methodName.Length > 3)
            core = methodName.Substring(3);

        if (core is null || core.Length == 0)
            return null;
        return char.ToUpperInvariant(core[0]) + core.Substring(1);
    }

    private static string? DeriveWrapperName(string methodName, string? explicitName)
    {
        // An explicit name may be used as the wrapper identifier only when it is a valid
        // C# identifier. Otherwise it is treated purely as a display label and the wrapper
        // is derived from the conventional "On" prefix.
        if (!string.IsNullOrWhiteSpace(explicitName) && IsValidIdentifier(explicitName!))
            return explicitName;
        if (methodName.StartsWith("On", System.StringComparison.Ordinal) && methodName.Length > 2
            && char.IsUpper(methodName[2]))
            return methodName.Substring(2);
        return null;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        if (!(char.IsLetter(value[0]) || value[0] == '_'))
            return false;
        for (var i = 1; i < value.Length; i++)
        {
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
                return false;
        }

        return true;
    }

    private static string Emit(StoreModel m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Nodes;");
        sb.AppendLine();

        var indent = "";
        if (m.Namespace is not null)
        {
            sb.AppendLine($"namespace {m.Namespace}");
            sb.AppendLine("{");
            indent = "    ";
        }

        sb.AppendLine($"{indent}partial class {m.ClassName} : global::Reflex.StoreBase");
        sb.AppendLine($"{indent}{{");

        var body = indent + "    ";

        // Name
        sb.AppendLine($"{body}/// <inheritdoc />");
        sb.AppendLine($"{body}public override string Name => \"{m.StoreName}\";");
        sb.AppendLine();

        // State properties
        foreach (var s in m.States)
        {
            sb.AppendLine($"{body}/// <summary>Reactive state backed by <c>{s.FieldName}</c>.</summary>");
            sb.AppendLine($"{body}public {s.TypeFqn} {s.PropertyName}");
            sb.AppendLine($"{body}{{");
            sb.AppendLine($"{body}    get => {s.FieldName};");
            sb.AppendLine($"{body}    set => SetState(ref {s.FieldName}, value, \"{s.PropertyName}\");");
            sb.AppendLine($"{body}}}");
            sb.AppendLine();
        }

        // Computed properties (memoized)
        foreach (var c in m.Computeds)
        {
            var validField = $"__{c.PropertyName}Valid";
            var valueField = $"__{c.PropertyName}Value";
            sb.AppendLine($"{body}private bool {validField};");
            sb.AppendLine($"{body}private {c.TypeFqn} {valueField} = default!;");
            sb.AppendLine($"{body}/// <summary>Memoized computed value from <c>{c.MethodName}()</c>.</summary>");
            sb.AppendLine($"{body}public {c.TypeFqn} {c.PropertyName}");
            sb.AppendLine($"{body}{{");
            sb.AppendLine($"{body}    get");
            sb.AppendLine($"{body}    {{");
            sb.AppendLine($"{body}        if (!{validField})");
            sb.AppendLine($"{body}        {{");
            sb.AppendLine($"{body}            {valueField} = {c.MethodName}();");
            sb.AppendLine($"{body}            {validField} = true;");
            sb.AppendLine($"{body}        }}");
            sb.AppendLine($"{body}        return {valueField};");
            sb.AppendLine($"{body}    }}");
            sb.AppendLine($"{body}}}");
            sb.AppendLine();
        }

        // Action wrappers
        foreach (var a in m.Actions)
        {
            var paramList = string.Join(", ", a.Parameters.Select(p => $"{p.TypeFqn} {p.Name}"));
            var argList = string.Join(", ", a.Parameters.Select(p => p.Name));
            sb.AppendLine($"{body}/// <summary>Dispatches the <c>{a.ActionLabel}</c> action.</summary>");
            if (a.IsAsync)
            {
                // ValueTask / ValueTask<T> aren't assignable to Task, so normalize via AsTask().
                var call = a.IsValueTask ? $"{a.ImplName}({argList}).AsTask()" : $"{a.ImplName}({argList})";
                sb.AppendLine($"{body}public global::System.Threading.Tasks.Task {a.WrapperName}({paramList})");
                sb.AppendLine($"{body}    => DispatchAsync(\"{a.ActionLabel}\", () => {call});");
            }
            else
            {
                sb.AppendLine($"{body}public void {a.WrapperName}({paramList})");
                sb.AppendLine($"{body}    => Dispatch(\"{a.ActionLabel}\", () => {a.ImplName}({argList}));");
            }

            sb.AppendLine();
        }

        // InvalidateComputed
        if (m.Computeds.Count > 0)
        {
            sb.AppendLine($"{body}/// <inheritdoc />");
            sb.AppendLine($"{body}protected override void InvalidateComputed()");
            sb.AppendLine($"{body}{{");
            foreach (var c in m.Computeds)
                sb.AppendLine($"{body}    __{c.PropertyName}Valid = false;");
            sb.AppendLine($"{body}}}");
            sb.AppendLine();
        }

        // SerializeState
        sb.AppendLine($"{body}/// <inheritdoc />");
        sb.AppendLine($"{body}public override JsonObject SerializeState()");
        sb.AppendLine($"{body}{{");
        sb.AppendLine($"{body}    var __o = new JsonObject();");
        foreach (var s in m.States)
            sb.AppendLine($"{body}    __o[\"{s.PropertyName}\"] = JsonSerializer.SerializeToNode({s.FieldName}, global::Reflex.ReflexJson.Options);");
        sb.AppendLine($"{body}    return __o;");
        sb.AppendLine($"{body}}}");
        sb.AppendLine();

        // DeserializeState
        sb.AppendLine($"{body}/// <inheritdoc />");
        sb.AppendLine($"{body}public override void DeserializeState(JsonObject state)");
        sb.AppendLine($"{body}{{");
        var idx = 0;
        foreach (var s in m.States)
        {
            var node = $"__n{idx++}";
            sb.AppendLine($"{body}    if (state.TryGetPropertyValue(\"{s.PropertyName}\", out var {node}) && {node} is not null)");
            sb.AppendLine($"{body}        {s.FieldName} = {node}.Deserialize<{s.TypeFqn}>(global::Reflex.ReflexJson.Options)!;");
        }

        sb.AppendLine($"{body}}}");

        sb.AppendLine($"{indent}}}");
        if (m.Namespace is not null)
            sb.AppendLine("}");

        return sb.ToString();
    }
}
