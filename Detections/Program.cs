using Huddly.Sdk;
using Huddly.Sdk.Detectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Samples.Shared;

namespace Detections;

internal class Program
{
    // With --once, how many batches of detections to read before exiting.
    private const int DetectionBatchesToRead = 5;

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
        // --once / --timeout handling, see Shared/SampleRun.cs. With --once, the sample exits
        // after reading a few batches of detections from the first device.
        var run = SampleRun.FromArgs(args, cts);

        // Create a separate cts specifically to cancel detectors.
        var detectorCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        // For signalling when detectors have been disposed properly
        var signal = new SemaphoreSlim(1, 1);

        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            if (!run.ShouldHandleDevice())
            {
                return;
            }

            var success = await HandleDeviceConnected(
                eventArgs.Device,
                run.Once ? DetectionBatchesToRead : null,
                detectorCts.Token,
                signal
            );

            // The detector has already been disposed at this point, so the sdk can go too.
            run.Complete(success);
        };

        Console.WriteLine("\n\nPress Control+C to quit the sample.\n\n");
        Console.CancelKeyPress += async (sender, eventArgs) =>
        {
            Console.WriteLine("Cancellation requested; will exit.");
            eventArgs.Cancel = true;

            // First cancel all running detectors
            detectorCts.Cancel();
            // Wait for the running detectors to be stopped and disposed properly
            await signal.WaitAsync(cts.Token);
            // Only after the detectors have been disposed do we cancel/dispose the sdk.
            cts.Cancel();
        };

        await huddlySdk.StartMonitoring(ct: cts.Token);
        await run.WaitUntilCancelled();
        return run.ExitCode;
    }

    private static async Task<bool> HandleDeviceConnected(
        IDevice device,
        int? maxDetections,
        CancellationToken ct,
        SemaphoreSlim signal
    )
    {
        Console.WriteLine($"Device {device} connected");

        await signal.WaitAsync(ct);
        try
        {
            return await ConsumeDetections(device, maxDetections, ct);
        }
        finally
        {
            // Release the signal to indicate that the detector has been disposed gracefully.
            signal.Release();
        }
    }

    private static async Task<bool> ConsumeDetections(
        IDevice device,
        int? maxDetections,
        CancellationToken ct
    )
    {
        var detectorOptions = DetectorOptions.DefaultFor(device.Model);
        detectorOptions.Mode = DetectorMode.AlwaysOn;

        var detectorResult = await device.GetDetector(detectorOptions, ct);
        if (!detectorResult.IsSuccess)
        {
            Console.WriteLine($"Could not create detector: {detectorResult.Message}");
            return false;
        }

        var detector = detectorResult.Value;
        var received = 0;
        var failed = false;
        try
        {
            // This loop continues until one of these happens:
            // 1. The token passed to GetDetections is cancelled.
            // 2. The detector is disposed.
            // 3. maxDetections batches have been received (only when set).
            // The IAsyncEnumerable returned by GetDetections does not throw in the first two cases.
            await foreach (Huddly.Sdk.Models.Detections detections in detector.GetDetections(ct))
            {
                var personBoxCount = detections.Count(detection => detection.Label == "person");
                var headBoxCount = detections.Count(detection => detection.Label == "head");
                Console.WriteLine(
                    $"Received detections with {personBoxCount} person boxes and {headBoxCount} head boxes"
                );

                if (++received == maxDetections)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Detector threw exception: {e.Message}");
            failed = true;
        }
        // Always dispose the detector properly after use.
        // This should be completed before disposing/canceling the ISdk from which the detector has been derived.
        await detector.DisposeAsync();

        // With maxDetections set (--once), all of them must arrive: the stream can also end early
        // without throwing. Otherwise at least one batch is enough.
        return !failed && received >= (maxDetections ?? 1);
    }
}
