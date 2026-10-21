using Huddly.Sdk;
using Huddly.Sdk.Detectors;
using Huddly.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Detections;

internal class Program
{
    // How many batches of detections to read before exiting when run with --once.
    private const int DetectionsToReadOnce = 5;

    // Pass --once to exit after reading a few detections from the first device: exit code 0 on
    // success, 1 on failure or if that doesn't happen within the timeout. Without it, runs until
    // Control+C.
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

        // Create a separate cts specifically to cancel detectors.
        var detectorCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        // For signalling when detectors have been disposed properly
        var signal = new SemaphoreSlim(1, 1);

        var deviceHandled = 0;
        huddlySdk.DeviceConnected += async (sender, eventArgs) =>
        {
            // With --once, only the first device that connects is handled.
            if (runOnce && Interlocked.Exchange(ref deviceHandled, 1) == 1)
            {
                return;
            }

            var success = await HandleDeviceConnected(
                eventArgs.Device,
                runOnce ? DetectionsToReadOnce : null,
                detectorCts.Token,
                signal
            );

            if (runOnce)
            {
                exitCode = success ? 0 : 1;
                // The detector has already been disposed at this point, so the sdk can go too.
                cts.Cancel();
            }
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
            // This loop will continue indefinitely until either:
            // 1. The token passed to GetDetections is cancelled.
            // 2. The detector is disposed.
            // 3. maxDetections batches have been received (only when set).
            // Note that the IAsyncEnumerable returned by GetDetections does not throw in the first two cases.
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

        return !failed && received > 0;
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
