using Huddly.Sdk;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Shared;

namespace FramingModes;

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

        huddlySdk.DeviceConnected += async (o, e) =>
        {
            if (!run.ShouldHandleDevice())
            {
                return;
            }

            var device = e.Device;

            // Get framing mode
            var getFramingResult = await device.GetFramingMode(cts.Token);
            FramingMode? framing = getFramingResult.IsSuccess ? getFramingResult.Value : null;
            Console.WriteLine($"Current framing value: {framing?.ToString() ?? "unknown"}");

            var supportedFeatures = await device.GetSupportedFeatures(cts.Token);

            Console.WriteLine($"Supported framing modes:");
            foreach (
                var supportedFraming in supportedFeatures.Value?.Framing
                    ?? Enumerable.Empty<FramingMode>()
            )
            {
                Console.WriteLine($"==== {supportedFraming}");
            }

            Console.WriteLine($"Changing framing mode to: {FramingMode.Manual}");
            var setManualResult = await device.SetFramingMode(FramingMode.Manual, cts.Token);
            if (setManualResult.IsSuccess)
            {
                Console.WriteLine("Successfully changed framing mode!");
            }

            Console.WriteLine($"Changing framing mode to: {FramingMode.GeniusFraming}");
            var setGeniusResult = await device.SetFramingMode(FramingMode.GeniusFraming, cts.Token);
            if (setGeniusResult.IsSuccess)
            {
                Console.WriteLine("Successfully changed framing mode!");
            }

            run.Complete(
                getFramingResult.IsSuccess
                    && supportedFeatures.IsSuccess
                    && setManualResult.IsSuccess
                    && setGeniusResult.IsSuccess
            );
        };
        huddlySdk.DeviceDisconnected += (o, e) =>
            Console.WriteLine($"Device {e.Device.Id} disconnected");

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
}
