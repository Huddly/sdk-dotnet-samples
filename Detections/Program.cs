﻿using Huddly.Sdk;
using Huddly.Sdk.Detectors;
using Huddly.Sdk.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Detections;

internal class Program
{
    static async Task Main(string[] args)
    {
        ISet<IDeviceMonitor> monitors = Huddly.Sdk.Monitor.DefaultIP(
            new NullLoggerFactory()
        );

        // Should always be disposed after use
        ISdk huddlySdk = Sdk.Create(new NullLoggerFactory(), monitors);

        var cts = new CancellationTokenSource();

        // Create a separate cts specifically for cancelling detectors.
        var detectorCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        // For signalling when detectors have been disposed properly
        var signal = new SemaphoreSlim(1, 1);

        huddlySdk.DeviceConnected += (sender, eventArgs) =>
            HandleDeviceConnected(sender, eventArgs, detectorCts.Token, signal);

        Console.WriteLine("Press Control+C to quit the sample.");
        Console.CancelKeyPress += async (sender, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested; will exit.");
            eventArgs.Cancel = true;

            // First cancel all running detectors
            detectorCts.Cancel();
            // Wait for the running detectors to be stopped and disposed properly
            await signal.WaitAsync();
            // Only after the detectors have been disposed do we cancel/dispose the sdk.
            cts.Cancel();
        };
        Task sdkTask = huddlySdk.StartMonitoring(ct: cts.Token);
        await sdkTask;
        huddlySdk.Dispose();
    }

    private static async void HandleDeviceConnected(
        object? sender,
        DeviceConnectionChangeEventArgs eventArgs,
        CancellationToken ct,
        SemaphoreSlim signal
    )
    {
        IDevice device = eventArgs.Device;
        Console.WriteLine($"Device {device} connected");

        await signal.WaitAsync();
        await ConsumeDetections(device, ct);
        // Release the signal to indicate that the detector has been disposed gracefully.
        signal.Release();
    }

    private static async Task ConsumeDetections(IDevice device, CancellationToken ct)
    {
        var detectorOptions = new DetectorOptions(
            DetectorMode.AlwaysOn,
            DetectionConvertion.Relative
        );
        Result<IDetector> detectorResult = await device.GetDetector(detectorOptions, ct);
        if (!detectorResult.IsSuccess)
        {
            Console.WriteLine($"Could not create detector: {detectorResult.Message}");
            return;
        }

        IDetector detector = detectorResult.Value;
        // This loop will continue indefinitely until either:
        // 1. The token passed to GetDetections is cancelled.
        // 2. The detector is disposed.
        // Note that the IAsyncEnumerable returned by GetDetections does not throw in either of these cases.
        await foreach (Huddly.Sdk.Models.Detections detections in detector.GetDetections(ct))
        {
            int personBoxCount = detections.Count(detection => detection.Label == "person");
            int headBoxCount = detections.Count(detection => detection.Label == "head");
            Console.WriteLine($"Received detections with {personBoxCount} person boxes and {headBoxCount} head boxes");
        }
        // Always dispose the detector properly after use.
        // This should be completed before disposing/cancelling the ISdk from which the detector has been derived.
        await detector.DisposeAsync();
    }
}
