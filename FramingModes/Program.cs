using Huddly.Device.Model;
using Huddly.Sdk;
using Huddly.Sdk.Extensions;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CameraInfo;
internal class Program
{
    static async Task Main(string[] args)
    {
        var ct = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Debug));

        services.AddHuddlySdk(
            configure =>
            {
                configure.UseUsbGrpcProxyClient();
                configure.UseUsbDeviceMonitor();
            }
        );

        var sp = services.BuildServiceProvider();

        // Should always be disposed after use
        using var huddlySdk = sp.GetRequiredService<ISdk>();

        IDevice? lastDevice = null;
        huddlySdk.DeviceConnected += async (o, d) =>
        {

            lastDevice = d;

            // Get framing mode
            var getFramingResult = await lastDevice.GetFraming();
            FramingValue? framing = getFramingResult.IsSuccess ? getFramingResult.Value : null;
            Console.WriteLine($"Current framing value: {getFramingResult.Value}");

            var supportedFeatures = await lastDevice.GetSupportedFeatures();

            if (supportedFeatures != null)
            {
                Console.WriteLine($"Supported framing modes:");
                foreach (FramingValue supportedFraming in supportedFeatures.Framing)
                {
                    Console.WriteLine($"==== {supportedFraming}");
                }
            }
            

            Console.WriteLine($"Changing framing mode to: {FramingValue.Manual}");
            var setFramingResult = await lastDevice.SetFraming(FramingValue.Manual);
            if (setFramingResult.IsSuccess)
            {
                Console.WriteLine("Succesfully changed framing mode!");
            }

            Console.WriteLine($"Changing framing mode to: {FramingValue.GeniusFraming}");
            setFramingResult = await lastDevice.SetFraming(FramingValue.GeniusFraming);
            if (setFramingResult.IsSuccess)
            {
                Console.WriteLine("Succesfully changed framing mode!");
            }

            Console.WriteLine("Press any key to quit...");
        };
        huddlySdk.DeviceDisconnected += (o, d) =>
        {
            Console.WriteLine($"Device {d.Id} disconnected");
            lastDevice = null;
        };
        var sdkStartTask = huddlySdk.StartMonitoring();

        Console.ReadKey();
    }
}
