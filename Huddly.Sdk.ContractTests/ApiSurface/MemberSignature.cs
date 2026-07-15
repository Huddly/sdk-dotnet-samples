namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// A formatted member line (see ApiSurfaceGenerator.DescribeMembers) split into the parts that
/// matter for contract comparison: its identity (kind + name + parameter types + return type -
/// the part that, if it changes, breaks a compiled caller), the set of attributes applied to it,
/// and whether it carries the `required` modifier.
///
/// Parameter names and default values are deliberately excluded from Identity: renaming a
/// parameter or changing its default doesn't break an existing compiled caller going through the
/// interface, so treating them as part of the identity would fail the build on a purely cosmetic
/// SDK change.
/// </summary>
internal readonly record struct MemberSignature(string Identity, IReadOnlyList<string> Attributes, bool Required)
{
    private const string RequiredMarker = "property required ";

    public static MemberSignature Parse(string memberLine)
    {
        var line = memberLine;
        var attributes = Array.Empty<string>();

        if (line.StartsWith('['))
        {
            var closeBracket = line.IndexOf("] ", StringComparison.Ordinal);
            if (closeBracket >= 0)
            {
                attributes = line[1..closeBracket].Split(", ", StringSplitOptions.RemoveEmptyEntries);
                line = line[(closeBracket + 2)..];
            }
        }

        var required = line.StartsWith(RequiredMarker, StringComparison.Ordinal);
        if (required)
            line = "property " + line[RequiredMarker.Length..];

        return new MemberSignature(StripParameterCosmetics(line), attributes, required);
    }

    /// <summary>
    /// For a method's "(...)" parameter list or an indexer's "[...]" index-parameter list, drops
    /// each parameter's name and default value, keeping only its by-ref modifier and type - the
    /// part that actually determines whether a compiled caller still binds to the member. No-op
    /// for non-indexer properties/events, which have no parameter list of their own.
    /// </summary>
    private static string StripParameterCosmetics(string identityLine)
    {
        var bracket = FindBracketPair(identityLine, '(', ')') ?? FindBracketPair(identityLine, '[', ']');
        if (bracket is not var (open, close))
            return identityLine;

        var parameters = SplitTopLevel(identityLine[(open + 1)..close], ',');
        var strippedParameters = parameters.Select(StripParameter);

        return identityLine[..(open + 1)] + string.Join(", ", strippedParameters) + identityLine[close..];
    }

    /// <summary>
    /// Finds the outermost bracket pair ending at the line's last closeChar, matching depth
    /// backward so an unrelated earlier bracket of the same kind - e.g. the array-type brackets in
    /// an indexer's "List&lt;string[]&gt; Item[int index]" - can't be mistaken for the real pair.
    /// </summary>
    private static (int Open, int Close)? FindBracketPair(string text, char openChar, char closeChar)
    {
        var close = text.LastIndexOf(closeChar);
        if (close < 0)
            return null;

        var depth = 0;
        for (var i = close; i >= 0; i--)
        {
            if (text[i] == closeChar)
                depth++;
            else if (text[i] == openChar && --depth == 0)
                return (i, close);
        }

        return null;
    }

    private static string StripParameter(string parameter)
    {
        var withoutDefault = parameter;
        var equals = parameter.IndexOf(" = ", StringComparison.Ordinal);
        if (equals >= 0)
            withoutDefault = parameter[..equals];

        var modifier = "";
        foreach (var candidate in new[] { "out ", "ref ", "in " })
        {
            if (withoutDefault.StartsWith(candidate, StringComparison.Ordinal))
            {
                modifier = candidate;
                withoutDefault = withoutDefault[candidate.Length..];
                break;
            }
        }

        var nameStart = LastTopLevelSpace(withoutDefault);
        var typeOnly = nameStart >= 0 ? withoutDefault[..nameStart] : withoutDefault;

        return modifier + typeOnly;
    }

    /// <summary>Splits on `separator` at angle-bracket depth 0, so generic type arguments (e.g. `Dictionary&lt;string, string&gt;`) aren't mistaken for separate parameters.</summary>
    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        if (text.Length == 0)
            return [];

        var depths = AngleBracketDepths(text);
        var parts = new List<string>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == separator && depths[i] == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(text[start..].Trim());
        return parts;
    }

    /// <summary>Finds the last space outside any angle-bracket nesting - the boundary between a parameter's type and its name.</summary>
    private static int LastTopLevelSpace(string text)
    {
        var depths = AngleBracketDepths(text);
        var lastSpace = -1;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ' && depths[i] == 0)
                lastSpace = i;
        }

        return lastSpace;
    }

    /// <summary>The angle-bracket nesting depth at each character, so callers can tell a top-level character (depth 0) from one inside a generic type argument list.</summary>
    private static int[] AngleBracketDepths(string text)
    {
        var depths = new int[text.Length];
        var depth = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
                depth++;
            else if (text[i] == '>')
                depth--;

            depths[i] = depth;
        }

        return depths;
    }
}
