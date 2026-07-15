using Huddly.Sdk.ContractTests.ApiSurface;

namespace Huddly.Sdk.ContractTests.Contract;

/// <summary>
/// Fails the build if any interface in ContractedInterfaces.txt has lost a previously published
/// member - a renamed/removed method, a method/property/event whose parameter types or return
/// type changed, an attribute removed from a member that still exists, or a member's `required`
/// modifier changing either way - relative to the checked-in baseline in Contract/ContractBaseline/.
///
/// Brand-new members and brand-new (additive) attributes are intentionally NOT treated as
/// failures here; they are surfaced separately as warnings, see NewMemberNotificationTests.
/// </summary>
public class PublicApiContractTests(ITestOutputHelper output)
{
    [Fact]
    public void ContractedInterfaces_HaveNotLostAnyPreviouslyPublishedMember()
    {
        if (BaselineRegenerator.RunIfEnabled())
        {
            output.WriteLine(BaselineRegenerator.RegeneratedMessage);
            return;
        }

        var testedVersion = TestedSdkVersion.Describe();
        output.WriteLine($"Tested against Huddly.Sdk {testedVersion}.");
        GitHubActionsAnnotations.Notice($"Contract tests ran against Huddly.Sdk {testedVersion}.");

        var (baseline, current) = ContractSnapshot.Load();

        var breakingChanges = ContractComparer.FindBreakingChanges(baseline, current);

        Assert.True(breakingChanges.Count == 0, BuildFailureMessage(breakingChanges, testedVersion));
    }

    private static string BuildFailureMessage(IReadOnlyList<ContractBreak> breakingChanges, string testedVersion)
    {
        var details = string.Join(Environment.NewLine, breakingChanges.Select(b => $"  - {b.InterfaceName}: {b.Description}"));

        return $"""
            Breaking change(s) detected in the public Huddly SDK contract (tested against Huddly.Sdk {testedVersion}):
            {details}

            If this change is intentional and has been approved by the team, run:
              HUDDLY_CONTRACT_UPDATE_BASELINE=1 dotnet test Huddly.Sdk.ContractTests
            then review the git diff of ContractedInterfaces.approved.txt and commit it.
            """;
    }
}
