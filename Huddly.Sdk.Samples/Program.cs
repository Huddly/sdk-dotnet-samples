namespace Huddly.Sdk.Samples;
using Huddly.Sdk;
using Huddly.Sdk.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal class Program
{
    static async Task Main(string[] args)
    {
        var ct = new CancellationTokenSource();
        var services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Debug));

        services.AddHuddlySdk(
            configure =>
            {
                configure.UseUsbGrpcProxyClient();
                configure.UseUsbDeviceMonitor();
            }
        );

        var sp = services.BuildServiceProvider();
        var huddlySdk = sp.GetRequiredService<ISdk>();

        IDevice? lastDevice = null;
        huddlySdk.DeviceConnected += async (o, d) =>
        {
            try
            {
                lastDevice = d;
                var features = await lastDevice.GetSupportedFeatures();
                Console.WriteLine(features);
              
                Console.WriteLine($"Device {d.Id} connected");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        };
        huddlySdk.DeviceDisconnected += (o, d) =>
        {
            Console.WriteLine($"Device {d.Id} disconnected");
            lastDevice = null;
        };
        var sdkStartTask = huddlySdk.StartMonitoring();
    }
}