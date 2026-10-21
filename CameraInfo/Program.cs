using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CameraInfo;

internal class Program
{
    // Pass --once to exit after the first device has been handled: exit code 0 on success, 1 on
    // failure or if no device connects within the timeout. Without it, runs until Control+C.
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

        IDevice? lastDevice = null;
        var deviceHandled = 0;
        huddlySdk.DeviceConnected += async (o, e) =>
        {
            // With --once, only the first device that connects is handled.
            if (runOnce && Interlocked.Exchange(ref deviceHandled, 1) == 1)
            {
                return;
            }

            lastDevice = e.Device;

            // Properties containing camera info
            var serialNumber = lastDevice.Serial;
            var manufacturer = lastDevice.Manufacturer;
            var deviceModel = lastDevice.Model;

            // Model name as a string
            var deviceNameResult = await lastDevice.GetName(cts.Token);
            var deviceName = deviceNameResult.IsSuccess ? deviceNameResult.Value : "Unknown";

            Console.WriteLine(
                $"Device type {deviceModel} with serial number {serialNumber} and name {deviceName} is manufactured by {manufacturer}."
            );

            // Device firmware version
            var fwVersionResult = await lastDevice.GetFirmwareVersion(cts.Token);
            if (fwVersionResult.IsSuccess)
            {
                Console.WriteLine($"Device firmware version: {fwVersionResult.Value}");
            }
            else
            {
                Console.WriteLine($"Failed reading firmware version: {fwVersionResult.Message}");
            }

            if (runOnce)
            {
                exitCode = deviceNameResult.IsSuccess && fwVersionResult.IsSuccess ? 0 : 1;
                cts.Cancel();
            }
        };
        huddlySdk.DeviceDisconnected += (o, e) =>
        {
            Console.WriteLine($"Device {e.Device.Id} disconnected");
            lastDevice = null;
        };

        Console.WriteLine("\n\nPress Control+C to quit the sample.\n\n");
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
