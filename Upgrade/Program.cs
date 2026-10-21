using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Upgrade;

internal class Program
{
    // Pass --once to exit after the first device has been handled: exit code 0 if it was upgraded
    // or already up to date, 1 on failure or if no device connects within the timeout. Without it,
    // runs until Control+C. Note that this really upgrades the device when a newer release exists.
    static async Task<int> Main(string[] args)
    {
        var runOnce = args.Contains("--once");
        var exitCode = 1;
        // --timeout <seconds> changes how long --once waits for a device (default: two minutes).
        var timeoutIndex = Array.IndexOf(args, "--timeout");
        var deviceTimeout = timeoutIndex >= 0
            ? TimeSpan.FromSeconds(int.Parse(args[timeoutIndex + 1]))
            : TimeSpan.FromMinutes(2);

        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Debug));

        services.AddHuddlySdk(configure =>
        {
            configure.UseUsbDeviceMonitor();
            configure.UseIpDeviceMonitor();
        });

        var sp = services.BuildServiceProvider();

        // Should always be disposed after use
        using var huddlySdk = sp.GetRequiredService<ISdk>();
        var cts = new CancellationTokenSource();

        var numDevicesConnected = 0;
        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            if (Interlocked.Increment(ref numDevicesConnected) > 1)
            {
                Console.WriteLine(
                    "Only running upgrade for the first device connected. Discarding connection event."
                );
                return;
            }

            if (runOnce)
            {
                // The timeout is only for finding a device; never cancel an upgrade midway.
                cts.CancelAfter(Timeout.InfiniteTimeSpan);
            }

            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");

            var success = await UpgradeRunner.UpgradeDeviceIfNewVersionIsAvailable(
                eventArgs.Device,
                cts.Token
            );

            if (runOnce)
            {
                exitCode = success ? 0 : 1;
                cts.Cancel();
            }
        };

        Console.WriteLine(
            "\n\nPress Control+C to quit the sample. "
                + "Note: Canceling an ongoing upgrade is not recommended.\n\n"
        );
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested; will exit.");
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        if (runOnce)
        {
            cts.CancelAfter(deviceTimeout);
        }

        await huddlySdk.StartMonitoring(ct: cts.Token);
        // Devices are reported through the events above while the SDK is monitoring. Depending on
        // the SDK version, StartMonitoring returns once monitoring has started rather than when it
        // is cancelled, so keep the sample alive until it is cancelled.
        await WaitForCancellation(cts.Token);
        return exitCode;
    }

    static async Task WaitForCancellation(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }
    }
}
