namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Reads the predefined list of interfaces considered part of the public SDK contract
/// from a text file (see ContractedInterfaces.txt).
/// </summary>
internal static class ContractedInterfaceCatalog
{
    public static IReadOnlyList<string> Load(string path)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (!seen.Add(line))
                throw new FormatException($"'{path}' lists '{line}' more than once - each contracted interface must appear exactly once.");

            names.Add(line);
        }

        return names;
    }
}
