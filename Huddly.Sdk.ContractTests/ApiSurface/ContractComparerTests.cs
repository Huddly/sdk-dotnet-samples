namespace Huddly.Sdk.ContractTests.ApiSurface;

public class ContractComparerTests
{
    [Fact]
    public void FindBreakingChanges_ReportsNothingWhenSurfacesAreIdentical()
    {
        var surface = Surface("A.IFoo", "interface IFoo", "method void Bar()");

        var breaks = ContractComparer.FindBreakingChanges([surface], [surface]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_ReportsARemovedMethod()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        var b = Assert.Single(breaks);
        Assert.Equal("A.IFoo", b.InterfaceName);
        Assert.Contains("method void Bar()", b.Description);
    }

    [Fact]
    public void FindBreakingChanges_ReportsARenamedMethodAsMissing()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo", "method void Baz()");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Single(breaks);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresAParameterRename()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar(int value)");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar(int amount)");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresAnIndexerParameterRename()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "property string Item[int index] { get; set; }");
        var current = Surface("A.IFoo", "interface IFoo", "property string Item[int i] { get; set; }");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresAnIndexerParameterRenameWhenTheIndexersOwnTypeContainsArrayBrackets()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "property List<string[]> Item[int index] { get; set; }");
        var current = Surface("A.IFoo", "interface IFoo", "property List<string[]> Item[int i] { get; set; }");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_ReportsAddingAGenericMethodTypeParameter()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method T0 Run<T0>(Func<T0> action)");
        var current = Surface("A.IFoo", "interface IFoo", "method T0 Run<T0, T1>(Func<T0> action)");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Single(breaks);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresADefaultValueChange()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar(int value = 1)");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar(int value = 2)");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_ReportsAParameterTypeChange()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar(int value)");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar(string value)");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Single(breaks);
    }

    [Fact]
    public void FindBreakingChanges_ReportsAByRefModifierAdded()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar(int value)");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar(out int value)");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Single(breaks);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresRenamesOnGenericParameterTypesContainingCommas()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar(Dictionary<string, string> queryParams, CancellationToken ct)");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar(Dictionary<string, string> parameters, CancellationToken cancellationToken)");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresAnAttributeAddedToAnExistingMethod()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo", "[Obsolete] method void Bar()");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_ReportsAnAttributeRemovedFromAnExistingMethod()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "[Obsolete] method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar()");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        var b = Assert.Single(breaks);
        Assert.Contains("Obsolete", b.Description);
    }

    [Fact]
    public void FindBreakingChanges_ReportsARequiredModifierAddedToAnExistingProperty()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "property string Name { get; init; }");
        var current = Surface("A.IFoo", "interface IFoo", "property required string Name { get; init; }");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        var b = Assert.Single(breaks);
        Assert.Contains("required", b.Description);
    }

    [Fact]
    public void FindBreakingChanges_ReportsARequiredModifierRemovedFromAnExistingProperty()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "property required string Name { get; init; }");
        var current = Surface("A.IFoo", "interface IFoo", "property string Name { get; init; }");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        var b = Assert.Single(breaks);
        Assert.Contains("required", b.Description);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresBrandNewMembers()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar()", "method void NewOne()");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindBreakingChanges_ReportsAnInterfaceThatNoLongerExists()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = new InterfaceSurface("A.IFoo", Found: false, Array.Empty<string>());

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        var b = Assert.Single(breaks);
        Assert.Contains("no longer exists", b.Description);
    }

    [Fact]
    public void FindBreakingChanges_IgnoresAnInterfaceThatWasAlreadyMissingInBaseline()
    {
        var baseline = new InterfaceSurface("A.IFoo", Found: false, Array.Empty<string>());
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar()");

        var breaks = ContractComparer.FindBreakingChanges([baseline], [current]);

        Assert.Empty(breaks);
    }

    [Fact]
    public void FindNotifications_ReportsANewMember()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar()", "method void NewOne()");

        var notices = ContractComparer.FindNotifications([baseline], [current]);

        var n = Assert.Single(notices);
        Assert.Equal("A.IFoo", n.InterfaceName);
        Assert.Contains("NewOne", n.Description);
    }

    [Fact]
    public void FindNotifications_ReportsANewOptionalAttributeOnAnExistingMember()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo", "[Obsolete] method void Bar()");

        var notices = ContractComparer.FindNotifications([baseline], [current]);

        var n = Assert.Single(notices);
        Assert.Contains("Obsolete", n.Description);
    }

    [Fact]
    public void FindNotifications_IgnoresUnchangedMembers()
    {
        var surface = Surface("A.IFoo", "interface IFoo", "method void Bar()");

        var notices = ContractComparer.FindNotifications([surface], [surface]);

        Assert.Empty(notices);
    }

    [Fact]
    public void FindNotifications_IgnoresAMemberLostFromAnExistingInterface()
    {
        var baseline = Surface("A.IFoo", "interface IFoo", "method void Bar()");
        var current = Surface("A.IFoo", "interface IFoo");

        var notices = ContractComparer.FindNotifications([baseline], [current]);

        Assert.Empty(notices);
    }

    [Fact]
    public void FindNotifications_TreatsAnUnbaselinedInterfaceAsAllNewMembers()
    {
        var baseline = new InterfaceSurface("A.IFoo", Found: false, Array.Empty<string>());
        var current = Surface("A.IFoo", "interface IFoo", "method void Bar()");

        var notices = ContractComparer.FindNotifications([baseline], [current]);

        Assert.Equal(2, notices.Count); // the interface header line and the method are both "new"
    }

    [Fact]
    public void RoundTrip_ThroughTextAndBackProducesEquivalentSurfaces()
    {
        var surfaces = new[]
        {
            Surface("A.IFoo", "interface IFoo", "method void Bar()"),
            new InterfaceSurface("A.IMissing", Found: false, Array.Empty<string>()),
        };

        var text = string.Join(Environment.NewLine + Environment.NewLine, surfaces.Select(s => s.ToText()));
        var parsed = ApiSurfaceBaselineParser.Parse(text);

        Assert.Equal(surfaces.Length, parsed.Count);
        for (var i = 0; i < surfaces.Length; i++)
        {
            Assert.Equal(surfaces[i].InterfaceName, parsed[i].InterfaceName);
            Assert.Equal(surfaces[i].Found, parsed[i].Found);
            Assert.Equal(surfaces[i].Members, parsed[i].Members);
        }
    }

    [Fact]
    public void Parse_IgnoresALeadingVersionHeaderComment()
    {
        var text = "# Generated against Huddly.Sdk 2.36.1" + Environment.NewLine + Environment.NewLine
            + "## A.IFoo" + Environment.NewLine + "interface IFoo" + Environment.NewLine + "method void Bar()";

        var parsed = ApiSurfaceBaselineParser.Parse(text);

        var surface = Assert.Single(parsed);
        Assert.Equal("A.IFoo", surface.InterfaceName);
        Assert.Equal(["interface IFoo", "method void Bar()"], surface.Members);
    }

    private static InterfaceSurface Surface(string name, params string[] members) =>
        new(name, Found: true, members);
}
