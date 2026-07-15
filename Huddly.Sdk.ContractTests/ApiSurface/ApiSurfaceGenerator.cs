using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Produces a stable textual description of the members (methods, properties, events,
/// attributes, required modifiers) declared directly on a predefined set of interfaces.
/// Two runs against an unchanged SDK version must produce byte-identical output; any
/// change to the described text represents a change to the interface's contract.
/// </summary>
internal static class ApiSurfaceGenerator
{
    public static IReadOnlyList<InterfaceSurface> Generate(
        IEnumerable<Assembly> assemblies,
        IReadOnlyCollection<string> contractedInterfaceNames)
    {
        var typesByName = new Dictionary<string, Type>();
        foreach (var type in assemblies.SelectMany(a => a.GetExportedTypes()).Where(t => t.IsInterface && (t.IsPublic || t.IsNestedPublic)))
        {
            if (!typesByName.TryAdd(type.FullName!, type))
                throw new InvalidOperationException($"Multiple exported interfaces share the full name '{type.FullName}' across the scanned assemblies.");
        }

        var nullability = new NullabilityInfoContext();

        return contractedInterfaceNames
            .Select(name => typesByName.TryGetValue(name, out var type)
                ? new InterfaceSurface(name, Found: true, DescribeMembers(type, nullability))
                : new InterfaceSurface(name, Found: false, Array.Empty<string>()))
            .OrderBy(s => s.InterfaceName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <paramref name="nullability"/> defaults to a fresh context when omitted (tests calling this
    /// directly for a single type don't need to care); Generate() passes one shared context across
    /// every contracted interface so its internal cache benefits from types repeated across them
    /// (CancellationToken, Task&lt;T&gt;, Result&lt;T&gt;, etc.).
    /// </summary>
    internal static IReadOnlyList<string> DescribeMembers(Type type, NullabilityInfoContext? nullability = null)
    {
        nullability ??= new NullabilityInfoContext();
        var lines = new List<string> { DescribeInterfaceHeader(type) };

        lines.AddRange(type.GetProperties()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => DescribeProperty(p, nullability)));

        lines.AddRange(type.GetEvents()
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .Select(DescribeEvent));

        lines.AddRange(type.GetMethods()
            .Where(m => !m.IsSpecialName)
            .Select(m => (m.Name, Text: DescribeMethod(m, nullability)))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.Text, StringComparer.Ordinal)
            .Select(m => m.Text));

        return lines;
    }

    private static string DescribeInterfaceHeader(Type type)
    {
        var attributes = FormatAttributes(type.GetCustomAttributesData());
        return $"{attributes}interface {type.Name}";
    }

    private static string DescribeProperty(PropertyInfo property, NullabilityInfoContext nullability)
    {
        var typeText = FormatType(property.PropertyType, SafeCreate(() => nullability.Create(property)));

        var accessors = new List<string>();
        if (property.GetMethod is not null)
            accessors.Add("get");
        if (property.SetMethod is not null)
            accessors.Add(IsInitOnly(property.SetMethod) ? "init" : "set");

        var requiredMarker = IsRequired(property) ? "required " : "";
        var attributes = FormatAttributes(property.GetCustomAttributesData());
        var indexer = DescribeIndexerParameters(property, nullability);

        return $"{attributes}property {requiredMarker}{typeText} {property.Name}{indexer} {{ {string.Join("; ", accessors)}; }}";
    }

    private static string DescribeIndexerParameters(PropertyInfo property, NullabilityInfoContext nullability)
    {
        var indexParameters = property.GetIndexParameters();
        if (indexParameters.Length == 0)
            return "";

        return "[" + string.Join(", ", indexParameters.Select(p => DescribeParameter(p, nullability))) + "]";
    }

    private static string DescribeEvent(EventInfo evt)
    {
        var attributes = FormatAttributes(evt.GetCustomAttributesData());
        var handlerType = evt.EventHandlerType is null ? "?" : FormatType(evt.EventHandlerType, null);
        return $"{attributes}event {handlerType} {evt.Name}";
    }

    private static string DescribeMethod(MethodInfo method, NullabilityInfoContext nullability)
    {
        Dictionary<Type, string>? genericParamNames = null;
        var generics = "";

        if (method.IsGenericMethodDefinition)
        {
            var typeParams = method.GetGenericArguments();
            genericParamNames = new Dictionary<Type, string>();
            for (var i = 0; i < typeParams.Length; i++)
                genericParamNames[typeParams[i]] = $"T{i}";

            generics = "<" + string.Join(", ", typeParams.Select(t => genericParamNames[t])) + ">";
        }

        var returnType = FormatType(method.ReturnType, SafeCreate(() => nullability.Create(method.ReturnParameter)), genericParamNames);
        var parameters = string.Join(", ", method.GetParameters().Select(p => DescribeParameter(p, nullability, genericParamNames)));
        var attributes = FormatAttributes(method.GetCustomAttributesData());

        return $"{attributes}method {returnType} {method.Name}{generics}({parameters})";
    }

    private static string DescribeParameter(
        ParameterInfo parameter,
        NullabilityInfoContext nullability,
        IReadOnlyDictionary<Type, string>? genericParamNames = null)
    {
        var modifier = parameter.IsOut ? "out " : parameter.IsIn ? "in " : IsByRef(parameter) ? "ref " : "";
        var typeText = FormatType(UnwrapByRef(parameter.ParameterType), SafeCreate(() => nullability.Create(parameter)), genericParamNames);
        var defaultText = parameter.HasDefaultValue ? $" = {FormatDefaultValue(parameter.DefaultValue)}" : "";

        return $"{modifier}{typeText} {parameter.Name}{defaultText}";
    }

    private static bool IsByRef(ParameterInfo parameter) => parameter.ParameterType.IsByRef;

    private static Type UnwrapByRef(Type type) => type.IsByRef ? type.GetElementType()! : type;

    private static bool IsInitOnly(MethodInfo setMethod) =>
        setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static bool IsRequired(PropertyInfo property) =>
        property.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");

    private static NullabilityInfo? SafeCreate(Func<NullabilityInfo> create)
    {
        try { return create(); }
        catch { return null; }
    }

    private static string FormatDefaultValue(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    private static string FormatAttributes(IEnumerable<CustomAttributeData> attributes)
    {
        var names = attributes
            .Where(a => a.AttributeType.Namespace is null
                        || !a.AttributeType.Namespace.StartsWith("System.Runtime.CompilerServices", StringComparison.Ordinal))
            .Where(a => a.AttributeType.FullName != "System.Reflection.DefaultMemberAttribute")
            .Select(a => Qualify(a.AttributeType, StripAttributeSuffix(a.AttributeType.Name)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return names.Count == 0 ? "" : $"[{string.Join(", ", names)}] ";
    }

    private static string StripAttributeSuffix(string name)
    {
        const string suffix = "Attribute";
        return name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }

    private static string FormatType(Type type, NullabilityInfo? info, IReadOnlyDictionary<Type, string>? genericParamNames = null)
    {
        if (type == typeof(void))
            return "void";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return FormatType(underlying, null, genericParamNames) + "?";

        string core;
        if (genericParamNames is not null && genericParamNames.TryGetValue(type, out var placeholder))
        {
            // type is one of the enclosing method's own generic type parameters (e.g. "TResult").
            // Rendered positionally instead of by its real name, same reasoning as stripping a
            // regular parameter's name: renaming a method's own type parameter doesn't break a
            // compiled caller, so its real name must not leak into the contract identity.
            core = placeholder;
        }
        else if (type.IsArray)
        {
            core = FormatType(type.GetElementType()!, info?.ElementType, genericParamNames) + "[]";
        }
        else if (type.IsGenericType)
        {
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick > 0)
                name = name[..tick];
            name = Qualify(type, name);

            var typeArgs = type.GetGenericArguments();
            var argInfos = info?.GenericTypeArguments;
            var args = typeArgs.Select((t, i) =>
                FormatType(t, argInfos is { Length: > 0 } && i < argInfos.Length ? argInfos[i] : null, genericParamNames));

            core = new StringBuilder(name).Append('<').AppendJoin(", ", args).Append('>').ToString();
        }
        else
        {
            core = KeywordAliases.TryGetValue(type, out var alias) ? alias : Qualify(type, type.Name);
        }

        if (info is not null && !type.IsValueType && info.ReadState == NullabilityState.Nullable)
            core += "?";

        return core;
    }

    /// <summary>
    /// Namespace-qualifies a rendered type name so two distinct CLR types that happen to share a
    /// simple name (e.g. an SDK-defined "Result" moved to a different sub-namespace) can never
    /// render identically and silently evade breaking-change detection. Left bare for
    /// System.*-namespaced types (Task, Dictionary, CancellationToken, etc.) since those are
    /// effectively global, stable, and would otherwise bloat every line of the baseline for a risk
    /// that doesn't apply to them.
    /// </summary>
    private static string Qualify(Type type, string simpleName) =>
        type.Namespace is not null && type.Namespace != "System" && !type.Namespace.StartsWith("System.", StringComparison.Ordinal)
            ? $"{type.Namespace}.{simpleName}"
            : simpleName;

    private static readonly Dictionary<Type, string> KeywordAliases = new()
    {
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
    };
}
