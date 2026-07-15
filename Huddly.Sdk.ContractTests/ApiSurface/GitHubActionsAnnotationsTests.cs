namespace Huddly.Sdk.ContractTests.ApiSurface;

public class GitHubActionsAnnotationsTests
{
    [Fact]
    public void Emit_SanitizesPercentAndNewlinesInTheTitle()
    {
        var writer = new StringWriter();

        WithGitHubActionsEnabled(() =>
            GitHubActionsAnnotations.Emit("notice", "message", "100% done\r\nnext line", writer));

        Assert.Equal("::notice title=100%25 done%0D%0Anext line::message" + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void Emit_SanitizesPercentAndNewlinesInTheMessage()
    {
        var writer = new StringWriter();

        WithGitHubActionsEnabled(() =>
            GitHubActionsAnnotations.Emit("warning", "50%\r\nfailed", null, writer));

        Assert.Equal("::warning::50%25%0D%0Afailed" + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void Emit_IsANoOpOutsideGitHubActions()
    {
        var writer = new StringWriter();
        var original = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", null);
            GitHubActionsAnnotations.Emit("notice", "message", null, writer);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", original);
        }

        Assert.Equal("", writer.ToString());
    }

    private static void WithGitHubActionsEnabled(Action action)
    {
        var original = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTIONS", original);
        }
    }
}
