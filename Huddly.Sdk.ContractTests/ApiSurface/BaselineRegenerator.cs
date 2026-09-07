namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Opt-in mechanism to accept the current SDK's contract shape as the new approved baseline,
/// instead of comparing against it. Enabled by setting the HUDDLY_CONTRACT_UPDATE_BASELINE
/// environment variable to "1", either locally or in the dedicated CI regeneration step in
/// contract-tests.yaml (a plain env value in the workflow file, not a GitHub secret) that runs
/// only after the gating contract check has already passed. That step writes into the checked-out
/// source tree but never merges the result directly - it lands as an uncommitted change that a
/// separate step commits straight to main, gated on that same passing check - so this can never
/// silently rewrite the baseline that gates a build.
///
/// Memoized (like ContractSnapshot) so PublicApiContractTests and NewMemberNotificationTests both
/// checking the flag in the same run doesn't result in two concurrent writes to the same file.
/// </summary>
internal static class BaselineRegenerator
{
    private const string EnableEnvironmentVariable = "HUDDLY_CONTRACT_UPDATE_BASELINE";

    private static readonly Lazy<bool> Ran = new(Regenerate);

    /// <summary>Shared by both gate tests so the message can't drift out of sync between them.</summary>
    public const string RegeneratedMessage =
        "HUDDLY_CONTRACT_UPDATE_BASELINE is set: regenerated the baseline from the current SDK. Review the git diff and commit it.";

    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnableEnvironmentVariable) == "1";

    /// <summary>Writes the current surface as the new approved baseline if enabled. Returns whether it ran.</summary>
    public static bool RunIfEnabled() => IsEnabled && Ran.Value;

    private static bool Regenerate()
    {
        var (_, current) = ContractSnapshot.Load();

        var header = $"# Generated against Huddly.Sdk {TestedSdkVersion.Describe()}";
        var body = string.Join(
            InterfaceSurface.NewLine + InterfaceSurface.NewLine,
            current.Select(surface => surface.ToText()));

        File.WriteAllText(FindBaselineSourcePath(), header + InterfaceSurface.NewLine + InterfaceSurface.NewLine + body + InterfaceSurface.NewLine);

        return true;
    }

    // AppContext.BaseDirectory is the build OUTPUT folder (bin/Debug/net10.0/...) - walking up to
    // the .csproj finds the real source tree, so the write lands somewhere `git diff` can see it.
    private static string FindBaselineSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddly.Sdk.ContractTests.csproj")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException($"Could not locate Huddly.Sdk.ContractTests.csproj above {AppContext.BaseDirectory}.");

        return Path.Combine(directory.FullName, "Contract", "ContractBaseline", "ContractedInterfaces.approved.txt");
    }
}
