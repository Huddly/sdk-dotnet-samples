using Huddly.Sdk;
using Huddly.Sdk.Models;

namespace Upgrade;

public class UpgradeRunner
{
    public static async Task UpgradeDeviceIfNewVersionIsAvailable(
        IDevice device,
        CancellationToken ct
    )
    {
        // Get information on the latest available release for the device
        var remoteFirmwareInfoResult = await device.FirmwareChecker.GetLatestRemoteVersion(
            FirmwareChannel.Stable,
            ct
        );
        if (!remoteFirmwareInfoResult.IsSuccess)
        {
            string errorMessage = remoteFirmwareInfoResult.Message;
            Console.WriteLine(
                $"Failed retrieving latest firmware version: {errorMessage}. Aborting."
            );
            return;
        }
        var remoteFirmwareInfo = remoteFirmwareInfoResult.Value;
        Console.WriteLine(
            $"Latest available firmware release is version {remoteFirmwareInfo.FirmwareVersion}"
        );

        // Check if the latest firmware is greater than the current device version
        // This step is optional: Upgrades can be performed to any version.
        var deviceFirmwareVersionResult = await device.GetFirmwareVersion(ct);
        if (!deviceFirmwareVersionResult.IsSuccess)
        {
            string errorMessage = deviceFirmwareVersionResult.Message;
            Console.WriteLine(
                $"Failed retrieving device firmware version: {errorMessage}. Aborting."
            );
            return;
        }
        var deviceFirmwareVersion = deviceFirmwareVersionResult.Value;
        if (deviceFirmwareVersion >= remoteFirmwareInfo.FirmwareVersion)
        {
            Console.WriteLine(
                "Latest firmware release is not greater than the current device firmware. Exiting"
            );
            return;
        }
        Console.WriteLine(
            "Latest firmware release is greater than the current device firmware. Proceeding with upgrade."
        );

        var httpClient = new HttpClient();
        using HttpResponseMessage response = await httpClient.GetAsync(
            remoteFirmwareInfo.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Failed trying to download firmware. Aborting.");
        }

        var firmwareFilePath = Path.GetTempFileName();
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
            await response.Content.CopyToAsync(fileStream, ct);
            Console.WriteLine("Firmware downloaded sucessfully.");
        }

        try
        {
            var deviceUpgrader = await device.GetFirmwareUpgrader(firmwareFilePath, ct);
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
            var upgradeResult = await deviceUpgrader.Execute(ct);
            if (!upgradeResult.IsSuccess)
            {
                Console.WriteLine($"Upgrade failed: {upgradeResult.Message}");
                return;
            }

            Console.WriteLine("Successfully upgraded device!");
        }
        finally
        {
            try
            {
                File.Delete(firmwareFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed deleting temporary firmware file: {ex.Message}");
            }
        }
    }
}
