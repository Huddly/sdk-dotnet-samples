using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceLogs;

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

        var deviceHandled = 0;
        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            // With --once, only the first device that connects is handled.
            if (runOnce && Interlocked.Exchange(ref deviceHandled, 1) == 1)
            {
                return;
            }

            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");
            var success = await RetrieveDeviceLogs(device, cts.Token);

            if (runOnce)
            {
                exitCode = success ? 0 : 1;
                cts.Cancel();
            }
        };
        huddlySdk.DeviceDisconnected += (sender, eventArgs) =>
            Console.WriteLine($"{eventArgs.Device.Id} disconnected");

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

    static async Task<bool> RetrieveDeviceLogs(IDevice device, CancellationToken ct)
    {
        // Device logs can be written to any stream. Here we use the console output.
        var outputStream = Console.OpenStandardOutput();
        Console.WriteLine($"---------- BEGIN DEVICE LOG FOR {device.Id} ---------- ");
        var getLogResult = await device.GetLog(outputStream, ct);
        Console.WriteLine($"---------- END DEVICE LOG FOR {device.Id} ---------- ");

        if (getLogResult.IsSuccess)
        {
            Console.WriteLine("Retrieved device logs successfully.");
            // Optional: After successfully retrieving and processing logs, it is often desirable to erase them.
            // Result eraseLogsResult = await device.EraseLog(ct);
        }
        else
        {
            Console.WriteLine($"Failed retrieving logs from device: {getLogResult.Message}.");
        }

        return getLogResult.IsSuccess;
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
