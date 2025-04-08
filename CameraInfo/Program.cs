using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CameraInfo;

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

            // Properties containing camera info
            var serialNumber = lastDevice.Serial;
            var manufacturer = lastDevice.Manufacturer;
            var deviceModel = lastDevice.Model;

            // Model name as a string
            var deviceNameResult = await lastDevice.GetName(cts.Token);
            var deviceName = deviceNameResult.IsSuccess ? deviceNameResult.Value : "Unknown";

            Console.WriteLine(
                $"Device type {deviceModel} with serial number {serialNumber} and name {deviceName} is manufactured by {manufacturer}."
            );

            // Device firmware version
            var fwVersion = (await lastDevice.GetFirmwareVersion(cts.Token)).GetValueOrThrow();
            Console.WriteLine($"Device firmware version: {fwVersion?.ToString() ?? "unknown"}");
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
