using Huddly.Sdk;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CameraInfo;

internal class Program
{
    static void Main(string[] _)
    {
        var cts = new CancellationTokenSource();
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

        IDevice? lastDevice = null;
        huddlySdk.DeviceConnected += async (o, e) =>
        {
            lastDevice = e.Device;

            // Get framing mode
            var getFramingResult = await lastDevice.GetFramingMode();
            FramingMode? framing = getFramingResult.IsSuccess ? getFramingResult.Value : null;
            Console.WriteLine($"Current framing value: {getFramingResult.Value}");

            var supportedFeatures = await lastDevice.GetSupportedFeatures();

            Console.WriteLine($"Supported framing modes:");
            foreach (FramingMode supportedFraming in supportedFeatures.Framing ?? Enumerable.Empty<FramingMode>())
            {
                Console.WriteLine($"==== {supportedFraming}");
            }

            Console.WriteLine($"Changing framing mode to: {FramingMode.Manual}");
            var setFramingResult = await lastDevice.SetFramingMode(FramingMode.Manual);
            if (setFramingResult.IsSuccess)
            {
                Console.WriteLine("Succesfully changed framing mode!");
            }

            Console.WriteLine($"Changing framing mode to: {FramingMode.GeniusFraming}");
            setFramingResult = await lastDevice.SetFramingMode(FramingMode.GeniusFraming);
            if (setFramingResult.IsSuccess)
            {
                Console.WriteLine("Succesfully changed framing mode!");
            }

            Console.WriteLine("Press any key to quit...");
        };
        huddlySdk.DeviceDisconnected += (o, e) =>
        {
            Console.WriteLine($"Device {e.Device.Id} disconnected");
            lastDevice = null;
        };
        var sdkStartTask = huddlySdk.StartMonitoring();

        Console.ReadKey();
    }
}
