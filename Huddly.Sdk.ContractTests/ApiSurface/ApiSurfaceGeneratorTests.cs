using System.Globalization;
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

public interface IArrayNullabilityFixture
{
    string[] NonNullableArray { get; }

    string[]? NullableArray { get; }
}

public interface IArrayRankFixture
{
    int[] Vector { get; }

    int[,] Matrix { get; }

    int[,,] Cube { get; }
}

public interface INullableEventFixture
{
    event EventHandler? Changed;
}

public interface IStaticMemberFixture
{
    static abstract int Count { get; }

    static abstract void Reset();
}

public interface IVariantFixture<out TResult, in TInput>
    where TResult : class
{
    TResult Convert(TInput input);
}

public interface IVariantFixtureRenamed<out T, in TArgument>
    where T : class
{
    T Convert(TArgument input);
}

public class GenericConstraintBase
{
}

public interface IGenericConstraintMarker
{
}

public interface IGenericConstraintFixture
{
    TResult Create<TResult>() where TResult : class, new();

    TResult CreateNullable<TResult>() where TResult : class?;

    TResult PreserveValue<TResult>() where TResult : struct;

    TResult PreserveUnmanaged<TResult>() where TResult : unmanaged;

    TResult CreateComplex<TResult>() where TResult : GenericConstraintBase, IGenericConstraintMarker, new();
}

public static class FirstNestedTypeHolder
{
    public sealed class Marker
    {
    }
}

public static class SecondNestedTypeHolder
{
    public sealed class Marker
    {
    }
}

public interface INestedTypeIdentityFixture
{
    FirstNestedTypeHolder.Marker GetFirst();

    SecondNestedTypeHolder.Marker GetSecond();
}

public interface IQualifyNamespaceFixture
{
    SystemDiagnosticsExtensions.IQualifyNamespaceFixtureMarker GetMarker();
}

public interface IGenericMethodFixture
{
    TResult Run<TResult>(Func<TResult> action);
}

public interface IGenericMethodFixtureRenamed
{
    T Run<T>(Func<T> action);
}

public interface ICultureSensitiveDefaultFixture
{
    void SetValue(double value = 1.5);
}

public class NestedInterfaceHolderFixture
{
    public interface INestedFixture
    {
        void DoSomething();
    }
}

public interface IOverloadSortFixture
{
    void Process(int a, int b);

    void Process(int a, string b);
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class CollisionAttribute : Attribute;

public interface IAttributeCollisionFixture
{
    [Collision]
    void LocalMarker();

    [SystemDiagnosticsExtensions.Collision]
    void RemoteMarker();
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
                "[Huddly.Sdk.ContractTests.ApiSurface.Sample] interface IGeneratorFixture",
                "property int Count { get; init; }",
                "property string Name { get; }",
                "event EventHandler Changed",
                "[Huddly.Sdk.ContractTests.ApiSurface.Sample] method Task<string?> DoWork(int amount, string label = \"default\")",
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
    public void DescribeMembers_DistinguishesNullableFromNonNullableArrays()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IArrayNullabilityFixture));

        Assert.Contains("property string[] NonNullableArray { get; }", members);
        Assert.Contains("property string[]? NullableArray { get; }", members);
    }

    [Fact]
    public void DescribeMembers_DistinguishesArrayRanks()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IArrayRankFixture));

        Assert.Contains("property int[] Vector { get; }", members);
        Assert.Contains("property int[,] Matrix { get; }", members);
        Assert.Contains("property int[,,] Cube { get; }", members);
    }

    [Fact]
    public void DescribeMembers_PreservesEventNullability()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(INullableEventFixture));

        Assert.Contains("event EventHandler? Changed", members);
    }

    [Fact]
    public void DescribeMembers_DistinguishesStaticMembers()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IStaticMemberFixture));

        Assert.Contains("property static int Count { get; }", members);
        Assert.Contains("method static void Reset()", members);
    }

    [Fact]
    public void DescribeMembers_PreservesGenericVarianceAndConstraints()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IVariantFixture<,>));

        Assert.Equal("interface IVariantFixture<out T0, in T1> where T0 : class", members[0]);
        Assert.Contains("method T0 Convert(T1? input)", members);

        var renamedMembers = ApiSurfaceGenerator.DescribeMembers(typeof(IVariantFixtureRenamed<,>));
        Assert.Equal(members[1], renamedMembers[1]);
    }

    [Fact]
    public void DescribeMembers_PreservesGenericMethodConstraints()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IGenericConstraintFixture));

        Assert.Contains("method T0 Create<T0>() where T0 : class, new()", members);
        Assert.Contains("method T0? CreateNullable<T0>() where T0 : class?", members);
        Assert.Contains("method T0 PreserveValue<T0>() where T0 : struct", members);
        Assert.Contains("method T0 PreserveUnmanaged<T0>() where T0 : unmanaged", members);
        Assert.Contains(
            "method T0 CreateComplex<T0>() where T0 : " +
            "Huddly.Sdk.ContractTests.ApiSurface.GenericConstraintBase, " +
            "Huddly.Sdk.ContractTests.ApiSurface.IGenericConstraintMarker, new()",
            members);
    }

    [Fact]
    public void DescribeMembers_QualifiesNestedTypesByTheirDeclaringType()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(INestedTypeIdentityFixture));

        Assert.Contains(
            "method Huddly.Sdk.ContractTests.ApiSurface.FirstNestedTypeHolder.Marker GetFirst()",
            members);
        Assert.Contains(
            "method Huddly.Sdk.ContractTests.ApiSurface.SecondNestedTypeHolder.Marker GetSecond()",
            members);
    }

    [Fact]
    public void DescribeMembers_QualifiesTypesFromNamespacesThatMerelyStartWithSystem()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IQualifyNamespaceFixture));

        Assert.Contains("method SystemDiagnosticsExtensions.IQualifyNamespaceFixtureMarker GetMarker()", members);
    }

    [Fact]
    public void DescribeMembers_RendersGenericMethodTypeParametersPositionally()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IGenericMethodFixture));

        // TResult is unconstrained (no "where TResult : notnull"/"class"), so it's genuinely
        // nullability-ambiguous under NRT analysis - the "?" reflects that correctly and predates
        // this fix; only the placeholder name ("T0") is new here.
        Assert.Contains("method T0? Run<T0>(Func<T0?> action)", members);
    }

    [Fact]
    public void DescribeMembers_RendersAGenericMethodTypeParameterRenameIdentically()
    {
        var original = ApiSurfaceGenerator.DescribeMembers(typeof(IGenericMethodFixture)).Single(m => m.StartsWith("method"));
        var renamed = ApiSurfaceGenerator.DescribeMembers(typeof(IGenericMethodFixtureRenamed)).Single(m => m.StartsWith("method"));

        Assert.Equal(original, renamed);
    }

    [Fact]
    public void DescribeMembers_FormatsDefaultValuesInvariantly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator - if FormatDefaultValue ever went back to a
            // plain culture-sensitive ToString(), this would render "1,5" instead of "1.5" and the
            // approved baseline would differ depending on which machine/locale generated it.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var members = ApiSurfaceGenerator.DescribeMembers(typeof(ICultureSensitiveDefaultFixture));

            Assert.Contains("method void SetValue(double value = 1.5)", members);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Generate_FindsNestedPublicInterfaces()
    {
        var nestedName = typeof(NestedInterfaceHolderFixture.INestedFixture).FullName!;

        var surfaces = ApiSurfaceGenerator.Generate([FixtureAssembly], [nestedName]);

        var surface = Assert.Single(surfaces);
        Assert.True(surface.Found, $"'{nestedName}' should have been found as a nested public interface.");
    }

    [Fact]
    public void DescribeMembers_SortsSameNameSameArityOverloadsDeterministicallyByFullSignature()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IOverloadSortFixture));

        var methodLines = members.Where(m => m.StartsWith("method")).ToList();
        Assert.Equal(
            [
                "method void Process(int a, int b)",
                "method void Process(int a, string b)",
            ],
            methodLines);
    }

    [Fact]
    public void DescribeMembers_QualifiesAttributesThatShareASimpleNameAcrossNamespaces()
    {
        var members = ApiSurfaceGenerator.DescribeMembers(typeof(IAttributeCollisionFixture));

        Assert.Contains("[Huddly.Sdk.ContractTests.ApiSurface.Collision] method void LocalMarker()", members);
        Assert.Contains("[SystemDiagnosticsExtensions.Collision] method void RemoteMarker()", members);
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
        var (_, current) = ContractSnapshot.Load();

        var missing = current.Where(s => !s.Found).Select(s => s.InterfaceName).ToList();
        Assert.True(missing.Count == 0, $"ContractedInterfaces.txt lists interfaces that no longer exist: {string.Join(", ", missing)}");
    }
}
