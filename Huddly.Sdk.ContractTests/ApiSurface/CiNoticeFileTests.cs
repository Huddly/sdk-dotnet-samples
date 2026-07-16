namespace Huddly.Sdk.ContractTests.ApiSurface;

public class CiNoticeFileTests
{
    [Fact]
    public void Append_WritesTheLineWhenThePathVariableIsSet()
    {
        var path = Path.GetTempFileName();
        var original = Environment.GetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE");
        try
        {
            Environment.SetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE", path);

            CiNoticeFile.Append("first");
            CiNoticeFile.Append("second");

            Assert.Equal(["first", "second"], File.ReadAllLines(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE", original);
            File.Delete(path);
        }
    }

    [Fact]
    public void Append_IsSafeUnderConcurrentCalls()
    {
        // Mirrors the real scenario: PublicApiContractTests and NewMemberNotificationTests are
        // different test classes, so xUnit runs them in different collections in parallel by
        // default, and both can call Append in the same run.
        var path = Path.GetTempFileName();
        var original = Environment.GetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE");
        try
        {
            Environment.SetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE", path);

            var lines = Enumerable.Range(0, 50).Select(i => $"line {i}").ToArray();
            Parallel.ForEach(lines, CiNoticeFile.Append);

            Assert.Equal(lines.Length, File.ReadAllLines(path).Length);
            Assert.Equal(lines.OrderBy(l => l), File.ReadAllLines(path).OrderBy(l => l));
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE", original);
            File.Delete(path);
        }
    }

    [Fact]
    public void Append_IsANoOpWhenThePathVariableIsNotSet()
    {
        var original = Environment.GetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE");
        try
        {
            Environment.SetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE", null);

            // Would throw (empty path) if Append ever tried to write without a configured path.
            CiNoticeFile.Append("should be dropped");
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUDDLY_CONTRACT_NOTICES_FILE", original);
        }
    }
}
