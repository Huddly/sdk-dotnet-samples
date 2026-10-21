namespace Huddly.Sdk.Samples.Tests;

/// <summary>
/// Runs every sample against a connected Huddly camera and checks that everything it demonstrates
/// succeeds (exit code 0).
///
/// Run with: dotnet test --filter Category=Device (any camera). Crew needs a Crew system
/// (Category=CrewDevice), and Upgrade is opt-in (Category=UpgradeDevice, see below).
/// </summary>
public class DeviceSampleTests(ITestOutputHelper output)
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    [Theory]
    [Trait("Category", "Device")]
    [InlineData("CameraInfo")]
    [InlineData("Detections")]
    [InlineData("DeviceLogs")]
    [InlineData("FramingModes")]
    public async Task Sample_succeeds_on_a_connected_device(string sample)
    {
        var (exitCode, _) = await SampleRunner.Run(sample, ProcessTimeout, output, "--once");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    [Trait("Category", "CrewDevice")]
    public async Task Crew_sample_succeeds_on_a_connected_crew_system()
    {
        var (exitCode, _) = await SampleRunner.Run("Crew", ProcessTimeout, output, "--once");

        Assert.Equal(0, exitCode);
    }

    // Really upgrades the camera when a newer firmware release exists, so it only runs when
    // HUDDLY_SAMPLES_ALLOW_UPGRADE=1 is set - never as a side effect of a plain `dotnet test`.
    [Fact]
    [Trait("Category", "UpgradeDevice")]
    public async Task Upgrade_sample_succeeds_on_a_connected_device()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("HUDDLY_SAMPLES_ALLOW_UPGRADE") == "1",
            "Set HUDDLY_SAMPLES_ALLOW_UPGRADE=1 to let this test upgrade the connected camera."
        );

        var (exitCode, _) = await SampleRunner.Run("Upgrade", TimeSpan.FromMinutes(30), output, "--once");

        Assert.Equal(0, exitCode);
    }
}
