using Huddly.Sdk;
using Huddly.Sdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace Upgrade;

internal class Program
{
    // For a robust implementation this collection should really be thread safe,
    // but this approach is used to keep the example simple
    private static readonly ISet<string> devicesCurrentlyUnderUpgrade = new HashSet<string>();

    static async Task Main(string[] args)
    {
        ISet<IDeviceMonitor> monitors = Huddly.Sdk.Monitor.DefaultFor(
            new NullLoggerFactory(),
            ConnectionType.Usb
        );

        // Should always be disposed after use
        ISdk huddlySdk = Sdk.Create(new NullLoggerFactory(), monitors);

        var cts = new CancellationTokenSource();

        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");
            if (devicesCurrentlyUnderUpgrade.Contains(device.Id))
            {
                Console.WriteLine(
                    $"Device {device.Id} is currently being upgraded. Discarding device connected event"
                );
                return;
            }
            devicesCurrentlyUnderUpgrade.Add(device.Id);
            await UpgradeRunner.UpgradeDeviceIfNewVersionIsAvailable(
                eventArgs.Device,
                cts.Token
            );
            devicesCurrentlyUnderUpgrade.Remove(device.Id);
        };

        Console.WriteLine("Press Control+C to quit the sample. Note: Cancelling an ongoing upgrade is not recommended.");
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested; will exit.");
            eventArgs.Cancel = true;
            cts.Cancel();
        };
        Task sdkTask = huddlySdk.StartMonitoring(ct: cts.Token);
        await sdkTask;
        huddlySdk.Dispose();
    }
}
