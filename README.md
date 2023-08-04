# Huddly .NET Software Development Kit (SDK)

The Huddly SDK is a library for interacting with Huddly devices. This repo contains documentation and common examples for interacting with the Huddly SDK.

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
Result<FramingMode> result = await device.GetFramingMode(FramingMode.SpeakerFraming);
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
