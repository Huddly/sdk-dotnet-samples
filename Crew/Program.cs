using Huddly.Device.Model;
using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crew;

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
            if (!(e.Device is { Model: DeviceModel.Crew } and IMultiCameraDevice))
            {
                Console.WriteLine($"Device {e.Device.Model} is not a Crew-device");
                return;
            }

            // Crew devices support most normal IDevice methods as seen in the other samples.
            // In addition, it has an API that is unique for only multicamera devices, which is demonstrated here

            lastDevice = e.Device;

            var crewDevice = (IMultiCameraDevice)e.Device;
            var connectedCamerasResult = await crewDevice.GetConnectedCameras(cts.Token);
            if (!connectedCamerasResult.IsSuccess)
            {
                Console.WriteLine(
                    $"Failed getting connected cameras: {connectedCamerasResult.Message}"
                );
                return;
            }
            var crewCameraStatuses = connectedCamerasResult.Value;
            Console.WriteLine($"Crew system with the following cameras connected:");
            foreach (var cameraStatus in crewCameraStatuses)
            {
                Console.WriteLine(
                    $"\tSerial: {cameraStatus.Serial}, Type: {cameraStatus.Type}, "
                        + $"Availability: {cameraStatus.Availability}"
                );
            }
        };
        huddlySdk.DeviceDisconnected += (o, e) =>
        {
            Console.WriteLine($"Device {e.Device.Id} disconnected");
            if (e.Device.Id == lastDevice?.Id)
            {
                lastDevice = null;
            }
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
