using XamantaSDK.Data.Stores;

namespace XamantaSDK.Services;

public class DeviceEraser(
    DeviceConnectionRegistry registry,
    DeviceStore devices,
    ILogger<DeviceEraser> logger)
{
    public async Task<bool> EraseAsync(string deviceId, string reason)
    {
        if (!await devices.DeleteAsync(deviceId))
        {
            logger.LogWarning("Nothing to erase for device {DeviceId} ({Reason})", deviceId, reason);
            return false;
        }

        registry.Disconnect(deviceId);
        logger.LogInformation("Erased device {DeviceId} ({Reason}): rows removed and streams closed",
            deviceId, reason);
        return true;
    }
}
