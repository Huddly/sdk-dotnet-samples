using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Shared;

namespace Upgrade;

internal class Program
{
    // Note that this really upgrades the device when a newer firmware release exists, also with
    // --once ("already up to date" counts as success).
    static async Task<int> Main(string[] args)
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
        // --once / --timeout handling, see Shared/SampleRun.cs. This sample upgrades only the first
        // device that connects, also without --once.
        var run = SampleRun.FromArgs(args, cts, onlyFirstDevice: true);

        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            if (!run.ShouldHandleDevice())
            {
                Console.WriteLine(
                    "Only running upgrade for the first device connected. Discarding connection event."
                );
                return;
            }

            IDevice device = eventArgs.Device;
            Console.WriteLine($"{device.Id} connected");

            var success = await UpgradeRunner.UpgradeDeviceIfNewVersionIsAvailable(
                eventArgs.Device,
                cts.Token
            );

            run.Complete(success);
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
        await run.WaitUntilCancelled();
        return run.ExitCode;
    }
}
