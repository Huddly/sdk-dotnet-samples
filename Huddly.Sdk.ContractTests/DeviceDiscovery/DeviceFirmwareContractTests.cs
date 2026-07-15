using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Huddly.Sdk.ContractTests.DeviceDiscovery;

/// <summary>
/// Exercises the real Huddly.Sdk device-discovery and firmware-read contract end to end against
/// a physically connected USB or network-reachable IP camera.
///
/// Unlike the reflection-based tests in ApiSurface/, this requires actual hardware and cannot run
/// on a normal hosted CI runner. It's tagged "Hardware" so it's excluded from the default
/// build/PR pipeline (see .github/workflows/contract-tests.yaml). This repo has no CI
/// infrastructure that reaches a real device - deliberately not borrowing sdk-dotnet's private
/// VPN/Jenkins bridge from a public repo - so run it manually on a machine with a device attached:
///   dotnet build Huddly.Sdk.ContractTests -c Release
///   dotnet Huddly.Sdk.ContractTests/bin/Release/*/Huddly.Sdk.ContractTests.dll -trait "Category=Hardware"
/// </summary>
[Trait("Category", "Hardware")]
public class DeviceFirmwareContractTests(ITestOutputHelper output)
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FirmwareReadTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task DiscoveredUsbOrIpDevice_ReportsAFirmwareVersion()
    {
        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddHuddlySdk(configure =>
        {
            configure.UseUsbDeviceMonitor();
            configure.UseIpDeviceMonitor();
        });

        await using var provider = services.BuildServiceProvider();
        using var sdk = provider.GetRequiredService<ISdk>();

        var deviceFound = new TaskCompletionSource<IDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        sdk.DeviceConnected += (_, e) => deviceFound.TrySetResult(e.Device);

        using var monitoringCts = new CancellationTokenSource();
        var monitoringTask = sdk.StartMonitoring(ct: monitoringCts.Token);

        try
        {
            using var discoveryTimeoutCts = new CancellationTokenSource(DiscoveryTimeout);
            using var discoveryTimeoutRegistration = discoveryTimeoutCts.Token.Register(() => deviceFound.TrySetCanceled());

            IDevice device;
            try
            {
                device = await deviceFound.Task;
            }
            catch (OperationCanceledException)
            {
                Assert.Fail(
                    $"No USB or IP device was discovered within {DiscoveryTimeout.TotalSeconds:0} seconds. " +
                    "This test requires a Huddly device connected over USB or reachable on the network.");
                return;
            }

            using var firmwareTimeoutCts = new CancellationTokenSource(FirmwareReadTimeout);
            var firmwareResult = await device.GetFirmwareVersion(firmwareTimeoutCts.Token);

            Assert.True(firmwareResult.IsSuccess, $"Failed to read firmware version from device {device.Serial}: {firmwareResult.Message}");
            Assert.False(string.IsNullOrWhiteSpace(firmwareResult.Value.ToString()));
        }
        finally
        {
            monitoringCts.Cancel();
            try
            {
                await monitoringTask;
            }
            catch (Exception ex)
            {
                // Swallow unconditionally: this is best-effort cleanup, and letting a shutdown
                // exception escape the finally block here would replace/mask a genuine assertion
                // failure raised above (e.g. the firmware-read check) with an unrelated one.
                output.WriteLine($"Ignoring exception while stopping device monitoring during cleanup: {ex}");
            }
        }
    }
}
