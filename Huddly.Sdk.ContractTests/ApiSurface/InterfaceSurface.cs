namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// The generated member surface for a single contracted interface, or a marker that the
/// interface could no longer be found in the scanned assemblies at all.
/// </summary>
internal sealed record InterfaceSurface(string InterfaceName, bool Found, IReadOnlyList<string> Members)
{
    public const string HeaderPrefix = "## ";
    public const string MissingSuffix = " [MISSING]";

    /// <summary>
    /// Fixed rather than Environment.NewLine, so the baseline file is byte-identical whether it
    /// was generated on Windows (developer running the local regeneration workflow) or Linux (the
    /// contract-tests.yaml runner) - otherwise every line would show as changed in a diff purely
    /// from a platform switch, with no actual contract change.
    /// </summary>
    public const string NewLine = "\n";

    public string ToText()
    {
        if (!Found)
            return $"{HeaderPrefix}{InterfaceName}{MissingSuffix}";

        return $"{HeaderPrefix}{InterfaceName}{NewLine}{string.Join(NewLine, Members)}";
    }
}
