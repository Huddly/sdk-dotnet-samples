namespace Huddly.Sdk.Samples.Tests;

/// <summary>
/// Runs every sample with no camera connected - as on a hosted CI runner - and checks that it
/// waits for a device and then reports failure with exit code 1, instead of crashing, hanging or
/// exiting successfully without having done anything.
///
/// Run with: dotnet test --filter Category=NoDevice (only on a machine without a Huddly camera).
/// </summary>
[Trait("Category", "NoDevice")]
public class NoDeviceSampleTests(ITestOutputHelper output)
{
    private static readonly TimeSpan DeviceTimeout = TimeSpan.FromSeconds(5);

    // Upgrade is deliberately left out: if this ever ran on a machine with a camera connected, it
    // would upgrade that camera's firmware.
    [Theory]
    [InlineData("CameraInfo")]
    [InlineData("Crew")]
    [InlineData("Detections")]
    [InlineData("DeviceLogs")]
    [InlineData("FramingModes")]
    public async Task Sample_reports_failure_when_no_device_connects(string sample)
    {
        var (exitCode, elapsed) = await SampleRunner.Run(
            sample,
            TimeSpan.FromMinutes(1),
            output,
            "--once",
            "--timeout",
            DeviceTimeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );

        Assert.Equal(1, exitCode);
        // Exiting before the timeout would mean the sample never waited for a device at all.
        Assert.True(elapsed >= DeviceTimeout, $"Sample {sample} exited after {elapsed}, before waiting {DeviceTimeout} for a device.");
    }
}
