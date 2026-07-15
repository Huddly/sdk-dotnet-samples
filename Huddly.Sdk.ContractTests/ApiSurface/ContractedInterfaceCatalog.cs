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

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            names.Add(line);
        }

        return names;
    }
}
