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
        var cts = new CancellationTokenSource();
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
        huddlySdk.DeviceConnected += async (o, e) =>
        {

            lastDevice = e.Device;

            // Properties containing camera info
            string serialNumber = lastDevice.Serial;
            Manufacturer manufacturer = lastDevice.Manufacturer;
            DeviceModel deviceModel = lastDevice.Model;

            // Model name as a string
            Result<string> deviceNameResult = await lastDevice.GetName();
            string deviceName = deviceNameResult.IsSuccess ? deviceNameResult.Value : "Unknown";

            Console.WriteLine($"Device type {deviceModel} with serial number {serialNumber} and name {deviceName} is manufactured by {manufacturer}.");
            Console.WriteLine("Press any key to quit...");
        };
        huddlySdk.DeviceDisconnected += (o, e) =>
        {
            Console.WriteLine($"Device {e.Device.Id} disconnected");
            lastDevice = null;
        };
        

        Console.ReadKey();
    }
}
