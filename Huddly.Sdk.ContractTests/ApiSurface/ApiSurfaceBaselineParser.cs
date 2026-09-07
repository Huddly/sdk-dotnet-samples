namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Parses the checked-in baseline file (produced by joining InterfaceSurface.ToText() blocks
/// with a blank line) back into InterfaceSurface instances for comparison against a freshly
/// generated surface.
///
/// Tolerates a leading "# comment" line/block (e.g. BaselineRegenerator's "# Generated against
/// Huddly.Sdk X.Y.Z" header) - anything starting with '#' but not the '## InterfaceName' block
/// marker is ignored rather than treated as a parse error.
/// </summary>
internal static class ApiSurfaceBaselineParser
{
    public static IReadOnlyList<InterfaceSurface> Parse(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var surfaces = new List<InterfaceSurface>();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !IsCommentLine(line))
                .ToArray();

            if (lines.Length == 0)
                continue; // a pure-comment block, e.g. the leading version header.

            if (!lines[0].StartsWith(InterfaceSurface.HeaderPrefix, StringComparison.Ordinal))
                throw new FormatException($"Expected a baseline block starting with '## InterfaceName', got: '{block}'");

            var header = lines[0][InterfaceSurface.HeaderPrefix.Length..];

            surfaces.Add(header.EndsWith(InterfaceSurface.MissingSuffix, StringComparison.Ordinal)
                ? new InterfaceSurface(header[..^InterfaceSurface.MissingSuffix.Length], Found: false, Array.Empty<string>())
                : new InterfaceSurface(header, Found: true, lines.Skip(1).ToList()));
        }

        return surfaces;
    }

    private static bool IsCommentLine(string line) =>
        line.StartsWith('#') && !line.StartsWith(InterfaceSurface.HeaderPrefix, StringComparison.Ordinal);
}
