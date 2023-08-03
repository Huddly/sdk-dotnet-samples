using Huddly.CameraProto;
using Huddly.Sdk;
using Huddly.Sdk.Detectors;
using Huddly.Sdk.Devices;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Detections;

internal class Program
{
    static int numDevicesConnected = 0;
    static async Task Main(string[] args)
    {
        ISet<IDeviceMonitor> monitors = Huddly.Sdk.Monitor.DefaultFor(new NullLoggerFactory(), ConnectionType.IP);

        // Should always be disposed after use
        ISdk huddlySdk = Sdk.Create(new NullLoggerFactory(), monitors);

        var cts = new CancellationTokenSource();
        var detectorCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        var signal = new SemaphoreSlim(0, 1);
        huddlySdk.DeviceConnected += (sender, eventArgs) => HandleDeviceConnected(sender, eventArgs, detectorCts.Token, signal);

        Console.WriteLine("Press Control+C to quit the sample.");
        Console.CancelKeyPress += async (sender, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested; will exit.");
            eventArgs.Cancel = true;

            Console.WriteLine("Tearing down detector");
            detectorCts.Cancel();
            await signal.WaitAsync();
            cts.Cancel();
        };
        Task sdkTask = huddlySdk.StartMonitoring(ct: cts.Token);
        await sdkTask;
        huddlySdk.Dispose();
    }

    private static async void HandleDeviceConnected(object? sender, DeviceConnectionChangeEventArgs eventArgs, CancellationToken ct, SemaphoreSlim signal)
    {
        if (numDevicesConnected++ > 0)
            return;
        IDevice device = eventArgs.Device;
        Console.WriteLine($"Device {device} connected");

        var detectorOptions = new DetectorOptions(DetectorMode.AlwaysOn, DetectionConvertion.Relative);
        Result<IDetector> detectorResult = await device.GetDetector(detectorOptions, ct);
        if (!detectorResult.IsSuccess)
        {
            Console.WriteLine($"Could not create detector: {detectorResult.Message}");
            return;
        }

        IDetector detector = detectorResult.Value;
        while (!ct.IsCancellationRequested)
        {
            await foreach (Huddly.Sdk.Models.Detections detections in detector.GetDetections(ct))
            {
                Console.WriteLine(detections.Count());
            }
        }

        signal.Release();
    }
}
