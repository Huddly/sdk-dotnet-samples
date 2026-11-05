using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Shared;

namespace DeviceLogs;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Debug));

        services.AddHuddlySdk(configure =>
        {
            configure.UseUsbDeviceMonitor();
            configure.UseIpDeviceMonitor();
        });

        // The ISdk should always be disposed after use. It is created by the service provider, so
        // disposing the provider disposes it (and flushes the console logger).
        using var sp = services.BuildServiceProvider();
        var huddlySdk = sp.GetRequiredService<ISdk>();
        var cts = new CancellationTokenSource();
        // --once / --timeout handling, see Shared/SampleRun.cs
        var run = SampleRun.FromArgs(args, cts);

        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            if (!run.ShouldHandleDevice())
            {
                return;
            }

            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");
            var success = await RetrieveDeviceLogs(device, cts.Token);

            run.Complete(success);
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

        await huddlySdk.StartMonitoring(ct: cts.Token);
        await run.WaitUntilCancelled();
        return run.ExitCode;
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
}
