using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Produces a stable textual description of the members (methods, properties, events,
/// attributes, nullability, static/required modifiers, generic constraints, and variance) declared
/// directly on a predefined set of interfaces, plus
/// members inherited from any base interface that isn't itself separately contracted (see
/// DescribeMembers). Two runs against an unchanged SDK version must produce byte-identical
/// output; any change to the described text represents a change to the interface's contract.
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
                ? new InterfaceSurface(name, Found: true, DescribeMembers(type, contractedInterfaceNames, nullability))
                : new InterfaceSurface(name, Found: false, Array.Empty<string>()))
            .OrderBy(s => s.InterfaceName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <paramref name="contractedInterfaceNames"/> defaults to none when omitted (tests describing
    /// a single fixture type don't need to care). <paramref name="nullability"/> defaults to a
    /// fresh context when omitted; Generate() passes one shared context across every contracted
    /// interface so its internal cache benefits from types repeated across them (CancellationToken,
    /// Task&lt;T&gt;, Result&lt;T&gt;, etc.).
    /// </summary>
    internal static IReadOnlyList<string> DescribeMembers(
        Type type,
        IReadOnlyCollection<string>? contractedInterfaceNames = null,
        NullabilityInfoContext? nullability = null)
    {
        nullability ??= new NullabilityInfoContext();
        contractedInterfaceNames ??= Array.Empty<string>();
        var typeGenericParamNames = BuildGenericParameterNames(type);

        // Reflection on an interface Type never walks its base interfaces - GetProperties/GetEvents/
        // GetMethods only return members declared directly on it. A base interface's members are
        // included here only when it ISN'T itself separately contracted, so a BCL interface (e.g.
        // IDisposable on ISdk) that can never have its own baseline entry still gets checked, without
        // duplicating coverage for SDK-authored base interfaces that already have their own entry.
        var typesToScan = new[] { type }
            .Concat(type.GetInterfaces().Where(i => !contractedInterfaceNames.Contains(i.FullName)))
            .ToArray();

        var lines = new List<string> { DescribeInterfaceHeader(type) };

        lines.AddRange(typesToScan.SelectMany(t => t.GetProperties())
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => DescribeProperty(p, nullability, typeGenericParamNames)));

        lines.AddRange(typesToScan.SelectMany(t => t.GetEvents())
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .Select(e => DescribeEvent(e, nullability, typeGenericParamNames)));

        lines.AddRange(typesToScan.SelectMany(t => t.GetMethods())
            .Where(m => !m.IsSpecialName)
            .Select(m => (m.Name, Text: DescribeMethod(m, nullability, typeGenericParamNames)))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.Text, StringComparer.Ordinal)
            .Select(m => m.Text));

        return lines;
    }

    private static string DescribeInterfaceHeader(Type type)
    {
        var attributes = FormatAttributes(type.GetCustomAttributesData());
        var name = StripGenericArity(type.Name);

        if (!type.IsGenericTypeDefinition)
            return $"{attributes}interface {name}";

        var typeParameters = type.GetGenericArguments();
        var parameterNames = BuildGenericParameterNames(type)!;
        var declaration = string.Join(", ", typeParameters.Select(parameter =>
            FormatVariance(parameter) + parameterNames[parameter]));

        return $"{attributes}interface {name}<{declaration}>{FormatGenericConstraints(typeParameters, parameterNames)}";
    }

    private static string DescribeProperty(
        PropertyInfo property,
        NullabilityInfoContext nullability,
        IReadOnlyDictionary<Type, string>? genericParamNames)
    {
        var typeText = FormatType(property.PropertyType, nullability.Create(property), genericParamNames);

        var accessors = new List<string>();
        if (property.GetMethod is not null)
            accessors.Add("get");
        if (property.SetMethod is not null)
            accessors.Add(IsInitOnly(property.SetMethod) ? "init" : "set");

        var requiredMarker = IsRequired(property) ? "required " : "";
        var staticMarker = property.GetMethod?.IsStatic == true || property.SetMethod?.IsStatic == true ? "static " : "";
        var attributes = FormatAttributes(property.GetCustomAttributesData());
        var indexer = DescribeIndexerParameters(property, nullability, genericParamNames);

        return $"{attributes}property {staticMarker}{requiredMarker}{typeText} {property.Name}{indexer} {{ {string.Join("; ", accessors)}; }}";
    }

    private static string DescribeIndexerParameters(
        PropertyInfo property,
        NullabilityInfoContext nullability,
        IReadOnlyDictionary<Type, string>? genericParamNames)
    {
        var indexParameters = property.GetIndexParameters();
        if (indexParameters.Length == 0)
            return "";

        return "[" + string.Join(", ", indexParameters.Select(p => DescribeParameter(p, nullability, genericParamNames))) + "]";
    }

    private static string DescribeEvent(
        EventInfo evt,
        NullabilityInfoContext nullability,
        IReadOnlyDictionary<Type, string>? genericParamNames)
    {
        var attributes = FormatAttributes(evt.GetCustomAttributesData());
        var handlerType = evt.EventHandlerType is null
            ? "?"
            : FormatType(evt.EventHandlerType, nullability.Create(evt), genericParamNames);
        var staticMarker = evt.AddMethod?.IsStatic == true || evt.RemoveMethod?.IsStatic == true ? "static " : "";
        return $"{attributes}event {staticMarker}{handlerType} {evt.Name}";
    }

    private static string DescribeMethod(
        MethodInfo method,
        NullabilityInfoContext nullability,
        IReadOnlyDictionary<Type, string>? typeGenericParamNames)
    {
        Dictionary<Type, string>? genericParamNames = typeGenericParamNames is null
            ? null
            : new Dictionary<Type, string>(typeGenericParamNames);
        var generics = "";

        if (method.IsGenericMethodDefinition)
        {
            var typeParams = method.GetGenericArguments();
            genericParamNames ??= new Dictionary<Type, string>();
            var prefix = typeGenericParamNames is null ? "T" : "M";
            for (var i = 0; i < typeParams.Length; i++)
                genericParamNames[typeParams[i]] = $"{prefix}{i}";

            generics = "<" + string.Join(", ", typeParams.Select(t => genericParamNames[t])) + ">";
        }

        var returnType = FormatType(method.ReturnType, nullability.Create(method.ReturnParameter), genericParamNames);
        var parameters = string.Join(", ", method.GetParameters().Select(p => DescribeParameter(p, nullability, genericParamNames)));
        var attributes = FormatAttributes(method.GetCustomAttributesData());
        var staticMarker = method.IsStatic ? "static " : "";
        var constraints = genericParamNames is null
            ? ""
            : FormatGenericConstraints(method.GetGenericArguments(), genericParamNames);

        return $"{attributes}method {staticMarker}{returnType} {method.Name}{generics}({parameters}){constraints}";
    }

    private static string DescribeParameter(
        ParameterInfo parameter,
        NullabilityInfoContext nullability,
        IReadOnlyDictionary<Type, string>? genericParamNames = null)
    {
        var modifier = parameter.IsOut ? "out " : parameter.IsIn ? "in " : IsByRef(parameter) ? "ref " : "";
        var typeText = FormatType(UnwrapByRef(parameter.ParameterType), nullability.Create(parameter), genericParamNames);
        var defaultText = parameter.HasDefaultValue ? $" = {FormatDefaultValue(parameter.DefaultValue)}" : "";

        return $"{modifier}{typeText} {parameter.Name}{defaultText}";
    }

    private static bool IsByRef(ParameterInfo parameter) => parameter.ParameterType.IsByRef;

    private static Type UnwrapByRef(Type type) => type.IsByRef ? type.GetElementType()! : type;

    private static bool IsInitOnly(MethodInfo setMethod) =>
        setMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    private static bool IsRequired(PropertyInfo property) =>
        property.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");

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
            var suffix = type.IsSZArray
                ? "[]"
                : $"[{new string(',', type.GetArrayRank() - 1)}]";
            core = FormatType(type.GetElementType()!, info?.ElementType, genericParamNames) + suffix;
        }
        else if (type.IsGenericType)
        {
            var name = StripGenericArity(type.Name);
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
        type.IsNested && type.DeclaringType is not null
            ? $"{Qualify(type.DeclaringType, StripGenericArity(type.DeclaringType.Name))}.{simpleName}"
            : type.Namespace is not null && type.Namespace != "System" && !type.Namespace.StartsWith("System.", StringComparison.Ordinal)
                ? $"{type.Namespace}.{simpleName}"
                : simpleName;

    private static string StripGenericArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick > 0 ? name[..tick] : name;
    }

    private static IReadOnlyDictionary<Type, string>? BuildGenericParameterNames(Type type)
    {
        if (!type.IsGenericTypeDefinition)
            return null;

        return type.GetGenericArguments()
            .Select((parameter, index) => (parameter, Name: $"T{index}"))
            .ToDictionary(pair => pair.parameter, pair => pair.Name);
    }

    private static string FormatVariance(Type genericParameter) =>
        (genericParameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask) switch
        {
            GenericParameterAttributes.Covariant => "out ",
            GenericParameterAttributes.Contravariant => "in ",
            _ => "",
        };

    private static string FormatGenericConstraints(
        IReadOnlyList<Type> typeParameters,
        IReadOnlyDictionary<Type, string> parameterNames)
    {
        var clauses = new List<string>();

        foreach (var parameter in typeParameters)
        {
            var constraints = new List<string>();
            var special = parameter.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
            var unmanaged = HasAttribute(parameter, "System.Runtime.CompilerServices.IsUnmanagedAttribute");

            if (unmanaged)
                constraints.Add("unmanaged");
            else if (special.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                constraints.Add(GetNullableAttributeFlag(parameter) == 2 ? "class?" : "class");
            else if (special.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                constraints.Add("struct");
            constraints.AddRange(parameter.GetGenericParameterConstraints()
                .Where(constraint => constraint != typeof(ValueType))
                .Select(constraint => FormatType(constraint, null, parameterNames))
                .OrderBy(constraint => constraint, StringComparer.Ordinal));

            if (special.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
                && !special.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
                clauses.Add($" where {parameterNames[parameter]} : {string.Join(", ", constraints)}");
        }

        return string.Concat(clauses);
    }

    private static bool HasAttribute(Type type, string fullName) =>
        type.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == fullName);

    private static byte? GetNullableAttributeFlag(Type type)
    {
        var attribute = type.GetCustomAttributesData()
            .FirstOrDefault(candidate => candidate.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (attribute is null || attribute.ConstructorArguments.Count == 0)
            return null;

        return attribute.ConstructorArguments[0].Value is byte flag ? flag : null;
    }

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
