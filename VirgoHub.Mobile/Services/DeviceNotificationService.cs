using VirgoHub.Shared;
using Microsoft.Maui.ApplicationModel;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
#endif

#if IOS
using Foundation;
using UserNotifications;
#endif

namespace VirgoHub.Mobile.Services;

public sealed class DeviceNotificationService : IDeviceNotificationService
{
    private const string ChannelId = "virgohub_updates";
    private const string ChannelName = "VirgoHub Updates";

    public async Task<bool> EnsurePermissionAsync()
    {
#if ANDROID
        EnsureAndroidChannel();

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<NotificationPermission>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<NotificationPermission>();
            }

            return status == PermissionStatus.Granted;
        }

        return true;
#elif IOS
        var result = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert |
            UNAuthorizationOptions.Sound |
            UNAuthorizationOptions.Badge);
        return result.Item1;
#else
        return true;
#endif
    }

    public Task ShowAsync(AppNotification notification)
    {
#if ANDROID
        EnsureAndroidChannel();
        var context = Android.App.Application.Context;
        if (context is null)
        {
            return Task.CompletedTask;
        }

        var builder = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle(notification.Title)
            .SetContentText(notification.Message)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(notification.Message))
            .SetPriority((int)NotificationPriority.High)
            .SetAutoCancel(true);

        NotificationManagerCompat.From(context).Notify(GetNotificationId(notification.Id), builder.Build());
        return Task.CompletedTask;
#elif IOS
        var content = new UNMutableNotificationContent
        {
            Title = notification.Title,
            Body = notification.Message,
            Sound = UNNotificationSound.Default
        };

        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(0.35, false);
        var request = UNNotificationRequest.FromIdentifier(notification.Id, content, trigger);
        return UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
#else
        return Task.CompletedTask;
#endif
    }

#if ANDROID
    private static void EnsureAndroidChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var context = Android.App.Application.Context;
        if (context is null)
        {
            return;
        }

        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager?.GetNotificationChannel(ChannelId) is not null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.High)
        {
            Description = "VirgoHub campus alerts and updates"
        };
        manager?.CreateNotificationChannel(channel);
    }

    private sealed class NotificationPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            [("android.permission.POST_NOTIFICATIONS", true)];
    }
#endif

    private static int GetNotificationId(string? id)
    {
        var hash = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().GetHashCode() : id.GetHashCode(StringComparison.Ordinal);
        return Math.Abs(hash == int.MinValue ? int.MaxValue : hash);
    }
}
