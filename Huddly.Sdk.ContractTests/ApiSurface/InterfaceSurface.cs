namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// The generated member surface for a single contracted interface, or a marker that the
/// interface could no longer be found in the scanned assemblies at all.
/// </summary>
internal sealed record InterfaceSurface(string InterfaceName, bool Found, IReadOnlyList<string> Members)
{
    public const string HeaderPrefix = "## ";
    public const string MissingSuffix = " [MISSING]";

    public string ToText()
    {
        if (!Found)
            return $"{HeaderPrefix}{InterfaceName}{MissingSuffix}";

        return $"{HeaderPrefix}{InterfaceName}{Environment.NewLine}{string.Join(Environment.NewLine, Members)}";
    }
}
