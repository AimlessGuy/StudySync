#if ANDROID
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace StudySync.Services;

public class AndroidSystemNotificationService : ISystemNotificationService
{
    private const string ChannelId = "studysync_peer_alerts";
    private const string ChannelName = "Peer Alerts";
    private const int NotificationId = 1001;

    public Task ShowPeerAlertAsync(string title, string message)
    {
        var context = Platform.AppContext;
        EnsureChannel(context);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            return Task.CompletedTask;
        }

        var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName);
        intent?.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop | ActivityFlags.NewTask);

        var pendingIntent = PendingIntent.GetActivity(
            context,
            NotificationId,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notification = new NotificationCompat.Builder(context, ChannelId)
            .SetSmallIcon(context.ApplicationInfo!.Icon)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
            .SetPriority((int)NotificationPriority.Default)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .Build();

        NotificationManagerCompat.From(context).Notify(NotificationId, notification);
        return Task.CompletedTask;
    }

    private static void EnsureChannel(Context context)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager?.GetNotificationChannel(ChannelId) != null)
            return;

        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Default)
        {
            Description = "StudySync peer activity alerts"
        };

        manager?.CreateNotificationChannel(channel);
    }
}
#endif
