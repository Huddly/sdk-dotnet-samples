using Huddly.Sdk;
using Huddly.Sdk.Models;
using Huddly.Sdk.Upgraders;

namespace Upgrade;

public class UpgradeRunner
{
    public static async Task UpgradeDeviceIfNewVersionIsAvailable(
        IDevice device,
        CancellationToken ct
    )
    {
        // Get information on the latest available release for the device
        Result<FirmwareInfo> firmwareReleaseInfoResult = await device.GetLatestFirmwareReleaseInfo(
            FirmwareChannel.Stable,
            ct
        );
        if (!firmwareReleaseInfoResult.IsSuccess)
        {
            string errorMessage = firmwareReleaseInfoResult.Message;
            Console.WriteLine($"Failed retrieving firmware release info: {errorMessage}. Aborting.");
            return;
        }
        FirmwareInfo firmwareReleaseInfo = firmwareReleaseInfoResult.Value;
        Console.WriteLine($"Latest available firmware release is version {firmwareReleaseInfo.Version}");

        // Check if the latest firmware is greater than the current device version
        // This step is optional: Upgrades can be performed to any version.
        Result<bool> newFirmwareReleaseIsAvailableResult = await device.IsGreaterThanCurrentVersion(
            firmwareReleaseInfo,
            ct
        );
        if (!newFirmwareReleaseIsAvailableResult.IsSuccess)
        {
            string errorMessage = firmwareReleaseInfoResult.Message;
            Console.WriteLine(
                $"Failed checking if latest firmware is greater than the current device version: {errorMessage}. Aborting."
            );
            return;
        }
        bool latestFirmwareIsNewerThanCurrentDeviceVersion = newFirmwareReleaseIsAvailableResult.Value;
        if (!latestFirmwareIsNewerThanCurrentDeviceVersion)
        {
            Console.WriteLine("Latest firmware release is not greater than the current device firmware. Aborting");
            return;
        }
        Console.WriteLine("Latest firmware release is greater than the current device firmware. Proceeding with upgrade.");
        
        HttpClient httpClient = new HttpClient();
        using HttpResponseMessage response = await httpClient.GetAsync(
            firmwareReleaseInfo.Url,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Failed trying to download firmware. Aborting.");
        }

        string firmwareFilePath = Path.GetTempFileName();
        using (
            var fileStream = new FileStream(
                firmwareFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None
            )
        )
        {
            Console.WriteLine("Downloading latest firmware from web server...");
            await response.Content.CopyToAsync(fileStream);
            Console.WriteLine("Firmware downloaded sucessfully.");
        }

        IFirmwareUpgrader deviceUpgrader = await device.GetFirmwareUpgrader(firmwareFilePath, ct);
        // Subscribe to this event before Executing the upgrade to see upgrade progress.
        deviceUpgrader.ProgressUpdated += (sender, e) =>
            Console.WriteLine(
                $"Upgrading device. Current state: {e.State}, progress: {e.ProgressPercent}"
            );

        Console.WriteLine("Starting device upgrade.");
        // When a firmware upgrade is run, the device will typically become non-responsive
        // for other methods. It is recommended to cease all other communication with the
        // device before executing an upgrade.
        // In the course of an upgrade, a device will disconnect and reconnect again. As
        // such, the original Huddly.Sdk.IDevice instance that was used to create the Huddly.Sdk.Upgraders.IFirmwareUpgrader
        // will disconnect. To continue communicating with the device when it has reconnected,
        // consumers should use the new Huddly.Sdk.IDevice instance emitted in the Huddly.Sdk.ISdk.DeviceConnected event
        Result upgradeResult = await deviceUpgrader.Execute(ct);
        if (!upgradeResult.IsSuccess)
        {
            Console.WriteLine("Upgrade failed");
        }

        Console.WriteLine("Successfully upgraded device!");
    }
}
