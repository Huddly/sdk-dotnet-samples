using System.Reflection;

namespace Huddly.Sdk.ContractTests.ApiSurface;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
internal sealed class SampleAttribute : Attribute;

[Sample]
public interface IGeneratorFixture
{
    string Name { get; }

    int Count { get; init; }

    event EventHandler Changed;

    [Sample]
    Task<string?> DoWork(int amount, string label = "default");
}

internal class RequiredPropertyFixture
{
    public required string Label { get; init; }
}

public interface IIndexerFixture
{
    string this[int index] { get; set; }
}

public class ApiSurfaceGeneratorTests
{
    private static readonly Assembly FixtureAssembly = typeof(IGeneratorFixture).Assembly;
    private static readonly string FixtureName = typeof(IGeneratorFixture).FullName!;

    [Fact]
    public void Generate_DescribesEveryMemberKindOfAKnownInterface()
    {
        var surfaces = ApiSurfaceGenerator.Generate([FixtureAssembly], [FixtureName]);

        var surface = Assert.Single(surfaces);
        Assert.True(surface.Found);
        Assert.Equal(
            [
                "[Sample] interface IGeneratorFixture",
                "property int Count { get; init; }",
                "property string Name { get; }",
                "event EventHandler Changed",
                "[Sample] method Task<string?> DoWork(int amount, string label = \"default\")",
            ],
            surface.Members);
    }

    [Fact]
    public void DescribeMembers_DetectsRequiredProperties()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(RequiredPropertyFixture));

        Assert.Contains("property required string Label { get; init; }", members);
    }

    [Fact]
    public void DescribeMembers_DetectsIndexerParameters()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IIndexerFixture));

        Assert.Contains("property string Item[int index] { get; set; }", members);
    }

    [Fact]
    public void DescribeMembers_FiltersOutTheCompilerGeneratedDefaultMemberAttribute()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IIndexerFixture));

        Assert.Equal("interface IIndexerFixture", members[0]);
    }

    [Fact]
    public void Generate_MarksMissingInterfacesInsteadOfThrowing()
    {
        var surfaces = ApiSurfaceGenerator.Generate([FixtureAssembly], ["Nonexistent.INotReal"]);

        var surface = Assert.Single(surfaces);
        Assert.False(surface.Found);
        Assert.Empty(surface.Members);
        Assert.Equal("## Nonexistent.INotReal [MISSING]", surface.ToText());
    }

    [Fact]
    public void Generate_IsDeterministicAcrossRuns()
    {
        var first = ApiSurfaceGenerator.Generate([FixtureAssembly], [FixtureName]).Single().ToText();
        var second = ApiSurfaceGenerator.Generate([FixtureAssembly], [FixtureName]).Single().ToText();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_ResolvesEveryContractedInterfaceAgainstTheRealSdk()
    {
        var contractedInterfaces = ContractedInterfaceCatalog.Load(
            Path.Combine(AppContext.BaseDirectory, "Contract", "ContractedInterfaces.txt"));

        var assemblies = new[] { typeof(ISdk).Assembly, typeof(Huddly.Device.Model.IDeviceDescriptor).Assembly };
        var surfaces = ApiSurfaceGenerator.Generate(assemblies, contractedInterfaces);

        var missing = surfaces.Where(s => !s.Found).Select(s => s.InterfaceName).ToList();
        Assert.True(missing.Count == 0, $"ContractedInterfaces.txt lists interfaces that no longer exist: {string.Join(", ", missing)}");
    }
}
