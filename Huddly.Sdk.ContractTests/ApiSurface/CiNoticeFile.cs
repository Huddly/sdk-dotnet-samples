namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>
/// Plain-text, append-only sink for CI notices (breaking-change failures and additive notices),
/// read back by contract-tests.yaml's Slack-notification step. No-ops unless the
/// HUDDLY_CONTRACT_NOTICES_FILE environment variable is set (mirrors GitHubActionsAnnotations'
/// own "no-op unless configured" pattern).
///
/// Deliberately plain text, not GitHub- or Slack-flavored markdown: GitHub markdown ("**bold**",
/// "# heading") and Slack's mrkdwn ("*bold*", no headings) aren't compatible, so the Slack step
/// builds its own formatting from this raw text instead of trying to reuse either dialect.
/// </summary>
internal static class CiNoticeFile
{
    private const string PathEnvironmentVariable = "HUDDLY_CONTRACT_NOTICES_FILE";

    // PublicApiContractTests and NewMemberNotificationTests are different test classes, so xUnit
    // runs them in different collections in parallel by default - both can call Append in the same
    // run (a breaking change and an additive notice can coexist), and File.AppendAllText alone
    // isn't safe against two threads opening the same file concurrently.
    private static readonly object WriteLock = new();

    public static void Append(string line)
    {
        var path = Environment.GetEnvironmentVariable(PathEnvironmentVariable);
        if (string.IsNullOrEmpty(path))
            return;

        lock (WriteLock)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
