namespace Huddly.Sdk.ContractTests.ApiSurface;

public class ContractedInterfaceCatalogTests
{
    [Fact]
    public void Load_SkipsBlankLinesAndComments()
    {
        var path = WriteTempFile("""
            # a comment

            A.IFoo
            # another comment
            A.IBar
            """);

        var names = ContractedInterfaceCatalog.Load(path);

        Assert.Equal(["A.IFoo", "A.IBar"], names);
    }

    [Fact]
    public void Load_ThrowsOnADuplicateEntry()
    {
        var path = WriteTempFile("""
            A.IFoo
            A.IBar
            A.IFoo
            """);

        var ex = Assert.Throws<FormatException>(() => ContractedInterfaceCatalog.Load(path));
        Assert.Contains("A.IFoo", ex.Message);
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
