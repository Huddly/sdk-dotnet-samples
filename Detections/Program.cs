using Huddly.Sdk;
using Huddly.Sdk.Detectors;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Detections;

internal class Program
{
    static async Task Main(string[] args)
    {
        ISet<IDeviceMonitor> monitors = Huddly.Sdk.Monitor.DefaultFor(new NullLoggerFactory(), ConnectionType.USB);

        // Should always be disposed after use
        ISdk huddlySdk = Sdk.Create(new NullLoggerFactory(), monitors);

        var cts = new CancellationTokenSource();
        huddlySdk.DeviceConnected += (sender, eventArgs) => HandleDeviceConnected(sender, eventArgs, cts.Token);

        Console.WriteLine("Press Control+C to quit the sample.");
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Cancellation requested; will exit.");
        };
        Task sdkTask = huddlySdk.StartMonitoring(ct: cts.Token);
        await sdkTask;
        huddlySdk.Dispose();
    }

    private static async void HandleDeviceConnected(object? sender, DeviceConnectionChangeEventArgs eventArgs, CancellationToken ct)
    {
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

        detector.Dispose();
    }
}
