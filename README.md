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
huddlySdk.DeviceConnected += async (sender, eventArgs) =>
{
    Console.WriteLine($"Device {eventArgs.Device.Model} connected");
}

huddlySdk.DeviceConnected += async (sender, eventArgs) =>
{
    Console.WriteLine($"Device {eventArgs.Device.Model} disconnected");
}
```

Then start monitoring

```csharp
CancellationTokenSource cts = new CancellationTokenSource();
huddlySdk.StartMonitoring(cts.Token)
```

If you have Huddly devices connected, you should now start getting connection events for them. Note that there can be some delay, particularly for IP devices.

## Running the samples

Each sample is a console app that keeps running until you press Control+C:

```
dotnet run --project CameraInfo
```

Pass `--once` to handle only the first device that connects and then exit. The exit code is `0` if everything the sample demonstrates succeeded, and non-zero on any failure, including no suitable device connecting within two minutes:

```
dotnet run --project CameraInfo -- --once
```

Note that `Upgrade --once` really upgrades the device when a newer firmware release is available, and `Crew` needs a Crew system. `--timeout <seconds>` changes how long `--once` waits for a device.

### Sample tests

`Huddly.Sdk.Samples.Tests` runs every sample with `--once` and checks its exit code:

| Command | Needs |
|---|---|
| `dotnet test --filter Category=NoDevice` | no Huddly camera connected; every sample must report failure |
| `dotnet test --filter Category=Device` | any Huddly camera; every sample must succeed |
| `dotnet test --filter Category=CrewDevice` | a Crew system |
| `dotnet test --filter Category=UpgradeDevice` | a camera you are willing to upgrade, and `HUDDLY_SAMPLES_ALLOW_UPGRADE=1` |

### SDK versions and tags

The samples always use one Huddly.Sdk version, set in `Directory.Packages.props`. On every Huddly.Sdk release, every sample is run against the new version on a real camera; only if they all pass, the samples are moved to that version and the commit is tagged with the release tag (e.g. `v2.38.0`). Check out that tag to get the samples that match the SDK version you use.

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
