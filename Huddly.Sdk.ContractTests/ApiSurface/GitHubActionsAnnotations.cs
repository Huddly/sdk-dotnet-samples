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

    private static void Emit(string level, string message, string? title)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
            return;

        var titlePart = title is null ? "" : $" title={title}";
        var sanitized = message.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
        Console.WriteLine($"::{level}{titlePart}::{sanitized}");
    }
}
