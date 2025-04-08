using Huddly.Sdk;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FramingModes;

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

        IDevice? lastDevice = null;
        huddlySdk.DeviceConnected += async (o, e) =>
        {
            lastDevice = e.Device;

            // Get framing mode
            var getFramingResult = await lastDevice.GetFramingMode(cts.Token);
            FramingMode? framing = getFramingResult.IsSuccess ? getFramingResult.Value : null;
            Console.WriteLine($"Current framing value: {getFramingResult.Value}");

            var supportedFeatures = await lastDevice.GetSupportedFeatures(cts.Token);

            Console.WriteLine($"Supported framing modes:");
            foreach (
                var supportedFraming in supportedFeatures.Value?.Framing
                    ?? Enumerable.Empty<FramingMode>()
            )
            {
                Console.WriteLine($"==== {supportedFraming}");
            }

            Console.WriteLine($"Changing framing mode to: {FramingMode.Manual}");
            var setFramingResult = await lastDevice.SetFramingMode(FramingMode.Manual, cts.Token);
            if (setFramingResult.IsSuccess)
            {
                Console.WriteLine("Successfully changed framing mode!");
            }

            Console.WriteLine($"Changing framing mode to: {FramingMode.GeniusFraming}");
            setFramingResult = await lastDevice.SetFramingMode(FramingMode.GeniusFraming, cts.Token);
            if (setFramingResult.IsSuccess)
            {
                Console.WriteLine("Successfully changed framing mode!");
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

        await huddlySdk.StartMonitoring(ct: cts.Token);
    }
}
