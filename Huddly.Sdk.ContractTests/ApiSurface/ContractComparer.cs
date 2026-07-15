namespace Huddly.Sdk.ContractTests.ApiSurface;

/// <summary>A breaking change detected between a baseline surface and the current one.</summary>
internal sealed record ContractBreak(string InterfaceName, string Description);

/// <summary>A non-breaking, additive change worth a human's attention before it's silently baselined.</summary>
internal sealed record ContractNotice(string InterfaceName, string Description);

/// <summary>
/// Compares a baseline surface against a freshly generated one.
///
/// Breaking (fails the build, see FindBreakingChanges): a contracted interface disappearing, a
/// member's identity (kind + name + parameter types + return type) disappearing - covering
/// renamed, removed, or resignatured methods/properties/events - an attribute being removed from
/// a member that still exists, or a member's `required` modifier changing either way.
///
/// Additive (reported, not failed, see FindNotifications): a brand-new member on a contracted
/// interface, or a brand-new attribute added to a member that still exists. Neither breaks a
/// compiled caller, but both extend what the team is implicitly promising and deserve review.
/// </summary>
internal static class ContractComparer
{
    public static IReadOnlyList<ContractBreak> FindBreakingChanges(
        IReadOnlyList<InterfaceSurface> baseline,
        IReadOnlyList<InterfaceSurface> current)
    {
        var breaks = new List<ContractBreak>();
        var currentByName = current.ToDictionary(s => s.InterfaceName);

        foreach (var baselineSurface in baseline)
        {
            if (!baselineSurface.Found)
                continue; // nothing was on record for this interface; nothing to lose.

            if (!currentByName.TryGetValue(baselineSurface.InterfaceName, out var currentSurface) || !currentSurface.Found)
            {
                breaks.Add(new ContractBreak(baselineSurface.InterfaceName, "interface no longer exists"));
                continue;
            }

            var currentSignatures = BuildSignatureIndex(currentSurface);

            foreach (var baselineMember in baselineSurface.Members)
            {
                var baselineSignature = MemberSignature.Parse(baselineMember);

                if (!currentSignatures.TryGetValue(baselineSignature.Identity, out var currentSignature))
                {
                    breaks.Add(new ContractBreak(baselineSurface.InterfaceName, $"missing or changed: {baselineSignature.Identity}"));
                    continue;
                }

                var removedAttributes = baselineSignature.Attributes.Except(currentSignature.Attributes, StringComparer.Ordinal).ToList();
                if (removedAttributes.Count > 0)
                {
                    breaks.Add(new ContractBreak(
                        baselineSurface.InterfaceName,
                        $"attribute(s) removed from {baselineSignature.Identity}: {string.Join(", ", removedAttributes)}"));
                }

                if (baselineSignature.Required != currentSignature.Required)
                {
                    breaks.Add(new ContractBreak(
                        baselineSurface.InterfaceName,
                        $"'required' modifier changed on {baselineSignature.Identity}"));
                }
            }
        }

        return breaks;
    }

    public static IReadOnlyList<ContractNotice> FindNotifications(
        IReadOnlyList<InterfaceSurface> baseline,
        IReadOnlyList<InterfaceSurface> current)
    {
        var notices = new List<ContractNotice>();
        var baselineByName = baseline.ToDictionary(s => s.InterfaceName);

        foreach (var currentSurface in current)
        {
            if (!currentSurface.Found)
                continue; // covered as a breaking change instead.

            var baselineSignatures = baselineByName.TryGetValue(currentSurface.InterfaceName, out var baselineSurface) && baselineSurface.Found
                ? BuildSignatureIndex(baselineSurface)
                : new Dictionary<string, MemberSignature>(StringComparer.Ordinal);

            foreach (var currentMember in currentSurface.Members)
            {
                var currentSignature = MemberSignature.Parse(currentMember);

                if (!baselineSignatures.TryGetValue(currentSignature.Identity, out var baselineSignature))
                {
                    notices.Add(new ContractNotice(currentSurface.InterfaceName, $"new member: {currentSignature.Identity}"));
                    continue;
                }

                var newAttributes = currentSignature.Attributes.Except(baselineSignature.Attributes, StringComparer.Ordinal).ToList();
                if (newAttributes.Count > 0)
                {
                    notices.Add(new ContractNotice(
                        currentSurface.InterfaceName,
                        $"new attribute(s) on {currentSignature.Identity}: {string.Join(", ", newAttributes)}"));
                }
            }
        }

        return notices;
    }

    private static Dictionary<string, MemberSignature> BuildSignatureIndex(InterfaceSurface surface) =>
        surface.Members.Select(MemberSignature.Parse).ToDictionary(s => s.Identity, StringComparer.Ordinal);
}
