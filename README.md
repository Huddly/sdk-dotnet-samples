# Huddly .NET Software Development Kit (SDK)

The Huddly SDK is a library for interacting with Huddly devices. This repo contains documentation and common examples for interacting with the Huddly SDK.

## Setup

The Huddly Sdk has two ways of discovering and identifying devices, IP and USB. Which one is appropriate depends on the device. When setting up the Sdk, you need to specify which of these protocols to use: You can use both, or just one of them.

We recommend using dependency injection through the `Huddly.Sdk.Extensions` package for creating an `ISdk` instance.

```csharp
services
    .AddHuddlySdk(
            configure =>
            {
                configure.UseUsbDeviceMonitor();
                configure.UseIpDeviceMonitor();
            }
        );
```

Alternatively, if you don't wish to use dependency injection

```csharp
using ISdk huddlySdk = Huddly.Sdk.Sdk.CreateDefault(new NullLoggerFactory());
```

or

```csharp
var usbMonitor = Huddly.Sdk.Monitor.UsbProxyClientDeviceMonitor();
var ipMonitor = Huddly.Sdk.Monitor.WsDiscoveryIpDeviceMonitor();

using var huddlySdk = new Sdk(new NullLoggerFactory(), [usbMonitor, ipMonitor]);
```

After creating an ISdk instance, add appropriate listeners for device connect/disconnect events:

```csharp
huddlySdk.DeviceConnected += (sender, eventArgs) =>
{
    Console.WriteLine($"Device {eventArgs.Device.Model} connected");
};

huddlySdk.DeviceDisconnected += (sender, eventArgs) =>
{
    Console.WriteLine($"Device {eventArgs.Device.Model} disconnected");
};
```

Then start monitoring

```csharp
CancellationTokenSource cts = new CancellationTokenSource();
await huddlySdk.StartMonitoring(ct: cts.Token);
```

If you have Huddly devices connected, you should now start getting connection events for them. Note that there can be some delay, particularly for IP devices.

`StartMonitoring` returns once monitoring has started, and devices keep being reported through the events until `cts` is cancelled. A console app therefore has to keep running after it, for example until Control+C, as the samples do.

### USB Proxy

The .NET Huddly SDK allows for connecting to USB devices through a proxy. This way, multiple independent processes can talk to a USB device at the same time. When communicating with USB devices, consumers should always default to attempt connecting to this proxy. If this fails, native USB can be used as a fallback. See the above examples for how to do this.

## Basic device communication

Devices can be interacted with using the `IDevice` interface. To obtain an `IDevice` instance, use the `DeviceConnected` event.

### `Result` and `Result<T>`

Device calls typically return a `Result` object which traps any potential exceptions raised during the interaction. To determine the outcome of an operation, use the `IsSuccess` property.

```csharp
Result result = await device.SetFramingMode(FramingMode.SpeakerFraming);
if (result.IsSuccess)
{
  Console.WriteLine("Framing mode set successfully");
}
else
{
  // When a result is not successful, it contains a message with diagnostic information
  Console.WriteLine($"Could not set framing mode, failed with status code {result.StatusCode} and message {result.Message}");
  // Alternatively, it will also contain an exception that can be used for the same purpose, or to be rethrown.
  Exception error = result.Error;
  _logger.LogError(error, "Could not set framing mode");
}
```

For operations where a value is included, Result<T> is used. Note that the contained value is safe to use if and only if the Result<T> was successful.

```csharp
Result<FramingMode> result = await device.GetFramingMode();
if (result.IsSuccess)
{
  // Operation was successful and we can safely retrieve the contained value
  FramingMode currentFramingMode = result.Value;
  Console.WriteLine($"Device is currently using {currentFramingMode}")
}
else
{
  // Handle error
}
```

Alternatively, if you prefer an exception based control flow, you can force the `Result` type to throw if the operation was not successful:

```csharp
Result result = await device.SetFramingMode(FramingMode.SpeakerFraming);
result.ThrowIfError();
```

```csharp
Result<FramingMode> result = await device.GetFramingMode();
FramingMode currentFramingMode = result.GetValueOrThrow();
```

## Running the samples

On Linux, install libusb first, which the SDK needs to find USB cameras (`sudo apt-get install libusb-1.0-0-dev` on Debian and Ubuntu).

Each sample is a console app that keeps running until you press Control+C:

```
dotnet run --project CameraInfo
```

Pass `--once` to handle only the first device that connects and then exit. The exit code is `0` if everything the sample demonstrates succeeded, and `1` on any failure, including no suitable device connecting within two minutes:

```
dotnet run --project CameraInfo -- --once
```

Note that `Upgrade --once` really upgrades the device when a newer firmware release is available, and `Crew` needs a Crew system. `--timeout <seconds>` changes how long `--once` waits for a device; once one connects, the sample takes as long as its work needs. Any other argument, or a bad `--timeout`, exits with code `2`. Both options are handled in `Shared/SampleRun.cs`, which every sample includes (through `Directory.Build.props`), so each sample's `Program.cs` only shows how the SDK is used.

### SDK versions and tags

The samples always use one Huddly.Sdk version, set in `Directory.Packages.props`. On every Huddly.Sdk release, the SDK's own pipeline runs the samples (all except Crew and Upgrade) with `--once` against the new version on a real camera; only if they all pass, the samples are moved to that version and the commit is tagged with the release tag (e.g. `v2.38.0`). Check out that tag to get the samples that match the SDK version you use.
