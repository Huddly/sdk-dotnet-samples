using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Upgrade;

internal class Program
{
    static async Task Main(string[] _)
    {
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

        int numDevicesConnected = 0;
        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            if (numDevicesConnected++ > 0)
            {
                Console.WriteLine(
                    "Only running upgrade for the first device connected. Discarding connection event."
                );
                return;
            }

            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");

            await UpgradeRunner.UpgradeDeviceIfNewVersionIsAvailable(eventArgs.Device, cts.Token);
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

        await huddlySdk.StartMonitoring(ct: cts.Token);
    }
}
