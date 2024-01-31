using Huddly.Sdk.Models;
using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceLogs;

internal class Program
{
    static async Task Main(string[] _)
    {
        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Debug));

        services.AddHuddlySdk(
            configure =>
            {
                configure.UseUsbDeviceMonitor();
                configure.UseIpDeviceMonitor();
            }
        );

        var sp = services.BuildServiceProvider();

        // Should always be disposed after use
        using var huddlySdk = sp.GetRequiredService<ISdk>();

        var cts = new CancellationTokenSource();

        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");
            await RetrieveDeviceLogs(device, cts.Token);
        };
        huddlySdk.DeviceDisconnected += (sender, eventArgs) =>
            Console.WriteLine($"{eventArgs.Device.Id} disconnected");

        var sdkStartTask = huddlySdk.StartMonitoring(ct: cts.Token);
        await sdkStartTask;

        huddlySdk.Dispose();
    }

    static async Task RetrieveDeviceLogs(IDevice device, CancellationToken ct)
    {
        // Device logs can be written to any stream. Here we use the console output.
        Stream outputStream = Console.OpenStandardOutput();
        Console.WriteLine($"---------- BEGIN DEVICE LOG FOR {device.Id} ---------- ");
        Result getLogResult = await device.GetLog(outputStream, ct);
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
    }
}
