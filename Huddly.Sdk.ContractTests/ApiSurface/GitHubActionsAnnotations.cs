namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Thin wrapper around GitHub Actions' workflow log commands, so a message is visible directly in
/// the Actions run UI (annotations/step summary) instead of only in the raw test output. No-ops
/// outside GitHub Actions.
/// </summary>
internal static class GitHubActionsAnnotations
{
    public static void Notice(string message, string? title = null) => Emit("notice", message, title);

    public static void Warning(string message, string? title = null) => Emit("warning", message, title);

    public static void Error(string message, string? title = null) => Emit("error", message, title);

    /// <summary>Internal (rather than private) and takes an optional writer so tests can capture
    /// output without redirecting the real, process-wide Console.Out.</summary>
    internal static void Emit(string level, string message, string? title, TextWriter? writer = null)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
            return;

        var titlePart = title is null ? "" : $" title={Sanitize(title)}";
        (writer ?? Console.Out).WriteLine($"::{level}{titlePart}::{Sanitize(message)}");
    }

    private static string Sanitize(string value) =>
        value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
}
