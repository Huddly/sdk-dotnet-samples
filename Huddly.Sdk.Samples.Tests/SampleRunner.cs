using System.Diagnostics;
using System.Reflection;

namespace Huddly.Sdk.Samples.Tests;

/// <summary>
/// Starts a built sample as its own process with the given arguments, streams its console output
/// to the test output, and returns its exit code and how long it ran.
/// </summary>
internal static class SampleRunner
{
    public static async Task<(int ExitCode, TimeSpan Elapsed)> Run(
        string sample,
        TimeSpan processTimeout,
        ITestOutputHelper output,
        params string[] arguments
    )
    {
        var samplePath = GetSamplePath(sample);
        Assert.True(File.Exists(samplePath), $"Sample {sample} has not been built: {samplePath} not found.");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(samplePath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => WriteLine(output, e.Data);
        process.ErrorDataReceived += (_, e) => WriteLine(output, e.Data);

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(processTimeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"Sample {sample} did not exit within {processTimeout}.");
        }

        return (process.ExitCode, stopwatch.Elapsed);
    }

    private static string GetSamplePath(string sample) =>
        Path.GetFullPath(
            Path.Combine(
                GetMetadata("SamplesDirectory"),
                sample,
                "bin",
                GetMetadata("SamplesConfiguration"),
                "net10.0",
                $"{sample}.dll"
            )
        );

    private static string GetMetadata(string key) =>
        typeof(SampleRunner)
            .Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == key)
            .Value!;

    private static void WriteLine(ITestOutputHelper output, string? line)
    {
        if (line is null)
        {
            return;
        }

        try
        {
            output.WriteLine(line);
        }
        catch (InvalidOperationException)
        {
            // The test has already finished; late output from the sample has nowhere to go.
        }
    }
}
