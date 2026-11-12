using VirgoHub.Shared;

namespace VirgoHub.Mobile.Services;

public interface IDeviceNotificationService
{
    Task<bool> EnsurePermissionAsync();

    Task ShowAsync(AppNotification notification);
}
