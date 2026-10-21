using System.Globalization;

namespace Samples.Shared;

/// <summary>
/// How a sample runs, shared by every sample so that each Program.cs only shows how the SDK is
/// used. This is not part of the Huddly SDK.
///
/// By default a sample handles every device that connects and runs until Control+C. With
/// <c>--once</c> it handles only the first device and then exits: exit code 0 if everything it
/// demonstrates succeeded, 1 on any failure or if no device connects within the device timeout
/// (two minutes, or <c>--timeout &lt;seconds&gt;</c>). The timeout only covers waiting for a
/// device: once one connects, the sample takes as long as its work needs.
/// Huddly.Sdk.Samples.Tests runs the samples this way.
/// </summary>
internal sealed class SampleRun
{
    private static readonly TimeSpan DefaultDeviceTimeout = TimeSpan.FromMinutes(2);

    // CancellationTokenSource.CancelAfter takes at most int.MaxValue milliseconds (about 24 days).
    private const int MaxTimeoutSeconds = int.MaxValue / 1000;

    private readonly CancellationTokenSource _cts;
    private readonly bool _onlyFirstDevice;
    private int _deviceClaimed;

    private SampleRun(bool once, bool onlyFirstDevice, CancellationTokenSource cts)
    {
        Once = once;
        _onlyFirstDevice = onlyFirstDevice;
        _cts = cts;
        // Without --once the sample runs until Control+C, which is a normal exit.
        ExitCode = once ? 1 : 0;
    }

    /// <summary>True when started with <c>--once</c>.</summary>
    public bool Once { get; }

    /// <summary>
    /// The sample's exit code. With <c>--once</c>: 1 until <see cref="Complete"/> reports success.
    /// Without it: 0.
    /// </summary>
    public int ExitCode { get; private set; }

    /// <summary>
    /// Reads <c>--once</c> and <c>--timeout &lt;seconds&gt;</c>. <paramref name="cts"/> is the
    /// sample's own token source: cancelling it ends the sample. With <c>--once</c>, the device
    /// timeout starts now, so call this before starting to monitor. Set
    /// <paramref name="onlyFirstDevice"/> for a sample that handles only the first device even
    /// without <c>--once</c>.
    /// </summary>
    public static SampleRun FromArgs(
        string[] args,
        CancellationTokenSource cts,
        bool onlyFirstDevice = false
    )
    {
        var deviceTimeout = DefaultDeviceTimeout;
        var timeoutIndex = Array.IndexOf(args, "--timeout");
        if (timeoutIndex >= 0)
        {
            var value = timeoutIndex + 1 < args.Length ? args[timeoutIndex + 1] : "";
            if (
                !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                || seconds is 0 or > MaxTimeoutSeconds
            )
            {
                throw new ArgumentException(
                    $"--timeout needs a whole number of seconds from 1 to {MaxTimeoutSeconds}, got '{value}'."
                );
            }
            deviceTimeout = TimeSpan.FromSeconds(seconds);
        }

        var run = new SampleRun(args.Contains("--once"), onlyFirstDevice, cts);
        if (run.Once)
        {
            cts.CancelAfter(deviceTimeout);
        }
        return run;
    }

    /// <summary>
    /// Whether to handle a device that just connected: always, unless the sample handles only the
    /// first device (<c>--once</c>, or <c>onlyFirstDevice</c>). With <c>--once</c>, claiming that
    /// device also stops the device timeout, so the work on it is never cut off halfway.
    /// </summary>
    public bool ShouldHandleDevice()
    {
        if (!Once && !_onlyFirstDevice)
        {
            return true;
        }

        if (Interlocked.Exchange(ref _deviceClaimed, 1) == 1)
        {
            return false;
        }

        if (Once)
        {
            _cts.CancelAfter(Timeout.InfiniteTimeSpan);
        }
        return true;
    }

    /// <summary>With <c>--once</c>, records the result of handling the device and ends the sample.</summary>
    public void Complete(bool success)
    {
        if (!Once)
        {
            return;
        }

        ExitCode = success ? 0 : 1;
        _cts.Cancel();
    }

    /// <summary>
    /// Keeps the sample running until it is cancelled. Devices are reported through the SDK's
    /// events while it is monitoring, and depending on the SDK version StartMonitoring returns as
    /// soon as monitoring has started rather than when it is cancelled.
    /// </summary>
    public async Task WaitUntilCancelled()
    {
        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException) { }
    }
}
