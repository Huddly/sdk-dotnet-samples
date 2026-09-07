using Huddly.Sdk.ContractTests.ApiSurface;

namespace Huddly.Sdk.ContractTests.Contract;

/// <summary>
/// Surfaces additive, non-breaking changes to the contracted interfaces - new methods/properties/
/// events, or new optional attributes on members that already existed - without failing the
/// build. These changes don't break a compiled caller, but the team never explicitly agreed to
/// support them, so they're worth a human looking at.
///
/// This test always passes; its job is visibility, not gatekeeping (see PublicApiContractTests
/// for the failing checks). It emits a GitHub Actions warning annotation per notice, and appends
/// one to CiNoticeSink (read back by contract-tests.yaml's Slack step), so they're visible even
/// though the job goes green - without turning this into a flaky gate.
/// </summary>
public class NewMemberNotificationTests(ITestOutputHelper output)
{
    [Fact]
    public void ContractedInterfaces_NewMembersAndAttributesAreReported()
    {
        if (BaselineRegenerator.RunIfEnabled())
        {
            output.WriteLine(BaselineRegenerator.RegeneratedMessage);
            return;
        }

        output.WriteLine($"Tested against Huddly.Sdk {TestedSdkVersion.Describe()}.");

        var (baseline, current) = ContractSnapshot.Load();

        var notices = ContractComparer.FindNotifications(baseline, current);

        if (notices.Count == 0)
        {
            output.WriteLine("No new members or attributes since the last approved baseline.");
            return;
        }

        output.WriteLine($"{notices.Count} additive, non-breaking change(s) found since the last approved baseline:");
        foreach (var notice in notices)
        {
            var message = $"{notice.InterfaceName}: {notice.Description}";
            output.WriteLine($"  - {message}");
            GitHubActionsAnnotations.Warning(message, "New Huddly SDK contract member");
            CiNoticeSink.Append($"⚠️ {message}");
        }

        output.WriteLine("""

            These are not build failures. Review them and, once accepted, regenerate
            Huddly.Sdk.ContractTests/Contract/ContractBaseline/ContractedInterfaces.approved.txt so this
            notice doesn't repeat on the next run.
            """);
    }
}
