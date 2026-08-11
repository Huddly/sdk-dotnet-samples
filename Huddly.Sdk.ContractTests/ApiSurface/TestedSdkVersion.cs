using System.Reflection;

namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// The exact Huddly.Sdk package version resolved at restore time.
///
/// Huddly.Sdk/Huddly.Sdk.Extensions float to the latest 2.x release in
/// Huddly.Sdk.ContractTests.csproj, so unlike every other dependency in this solution the version
/// isn't fixed in source control - a nightly run can test a different SDK release than the one
/// from last night. Surfacing it here means a contract-test failure is traceable to a specific
/// SDK version instead of just "it broke".
/// </summary>
internal static class TestedSdkVersion
{
    public static string Describe() =>
        typeof(ISdk).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ISdk).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}
