using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Shared;

namespace CameraInfo;

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

            // Properties containing camera info
            var serialNumber = device.Serial;
            var manufacturer = device.Manufacturer;
            var deviceModel = device.Model;

            // Model name as a string
            var deviceNameResult = await device.GetName(cts.Token);
            var deviceName = deviceNameResult.IsSuccess ? deviceNameResult.Value : "Unknown";

            Console.WriteLine(
                $"Device type {deviceModel} with serial number {serialNumber} and name {deviceName} is manufactured by {manufacturer}."
            );

            // Device firmware version
            var fwVersionResult = await device.GetFirmwareVersion(cts.Token);
            if (fwVersionResult.IsSuccess)
            {
                Console.WriteLine($"Device firmware version: {fwVersionResult.Value}");
            }
            else
            {
                Console.WriteLine($"Failed reading firmware version: {fwVersionResult.Message}");
            }

            run.Complete(deviceNameResult.IsSuccess && fwVersionResult.IsSuccess);
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
