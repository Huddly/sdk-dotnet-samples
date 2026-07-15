using System.Reflection;

namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Loads the checked-in baseline and the freshly generated surface for every interface listed
/// in ContractedInterfaces.txt, against the SDK assemblies actually referenced by this project.
///
/// Memoized: the SDK assemblies loaded into this process don't change between tests, so every
/// caller within a run shares one file read and one reflection pass instead of repeating both.
/// </summary>
internal static class ContractSnapshot
{
    private static readonly Assembly[] SdkAssemblies =
    [
        typeof(ISdk).Assembly,
        typeof(Huddly.Device.Model.IDeviceDescriptor).Assembly,
    ];

    private static readonly Lazy<(IReadOnlyList<InterfaceSurface> Baseline, IReadOnlyList<InterfaceSurface> Current)> Snapshot =
        new(LoadUncached);

    public static (IReadOnlyList<InterfaceSurface> Baseline, IReadOnlyList<InterfaceSurface> Current) Load() => Snapshot.Value;

    private static (IReadOnlyList<InterfaceSurface> Baseline, IReadOnlyList<InterfaceSurface> Current) LoadUncached()
    {
        var contractedInterfaces = ContractedInterfaceCatalog.Load(
            Path.Combine(AppContext.BaseDirectory, "Contract", "ContractedInterfaces.txt"));

        var baseline = ApiSurfaceBaselineParser.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contract", "ContractBaseline", "ContractedInterfaces.approved.txt")));

        var current = ApiSurfaceGenerator.Generate(SdkAssemblies, contractedInterfaces);

        return (baseline, current);
    }
}
