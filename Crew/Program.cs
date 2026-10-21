using Huddly.Device.Model;
using Huddly.Sdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crew;

internal class Program
{
    // Pass --once to exit after the first Crew device has been handled: exit code 0 on success, 1
    // on failure or if no Crew device connects within the timeout. Without it, runs until Control+C.
    static async Task<int> Main(string[] args)
    {
        var runOnce = args.Contains("--once");
        var exitCode = 1;
        // --timeout <seconds> changes how long --once waits for a device (default: two minutes).
        var timeoutIndex = Array.IndexOf(args, "--timeout");
        var deviceTimeout = timeoutIndex >= 0
            ? TimeSpan.FromSeconds(int.Parse(args[timeoutIndex + 1]))
            : TimeSpan.FromMinutes(2);

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
        var deviceHandled = 0;
        huddlySdk.DeviceConnected += async (o, e) =>
        {
            if (!(e.Device is { Model: DeviceModel.Crew } and IMultiCameraDevice))
            {
                Console.WriteLine($"Device {e.Device.Model} is not a Crew-device");
                return;
            }

            // With --once, only the first Crew device that connects is handled.
            if (runOnce && Interlocked.Exchange(ref deviceHandled, 1) == 1)
            {
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
            }
            else
            {
                var crewCameraStatuses = connectedCamerasResult.Value;
                Console.WriteLine($"Crew system with the following cameras connected:");
                foreach (var cameraStatus in crewCameraStatuses)
                {
                    Console.WriteLine(
                        $"\tSerial: {cameraStatus.Serial}, Type: {cameraStatus.Type}, "
                            + $"Availability: {cameraStatus.Availability}"
                    );
                }
            }

            if (runOnce)
            {
                exitCode = connectedCamerasResult.IsSuccess ? 0 : 1;
                cts.Cancel();
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

        if (runOnce)
        {
            cts.CancelAfter(deviceTimeout);
        }

        await huddlySdk.StartMonitoring(ct: cts.Token);
        // Devices are reported through the events above while the SDK is monitoring. Depending on
        // the SDK version, StartMonitoring returns once monitoring has started rather than when it
        // is cancelled, so keep the sample alive until it is cancelled.
        await WaitForCancellation(cts.Token);
        return exitCode;
    }

    static async Task WaitForCancellation(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException) { }
    }
}
