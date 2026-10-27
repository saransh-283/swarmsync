using Android.App;
using Android.Content;
using Android.OS;

namespace SwarmSync.Android;

[Service(Name = "com.swarmsync.android.DaemonService", Enabled = true, Exported = false)]
public class DaemonService : Service
{
    private const int NotificationId = 1001;
    private const string ChannelId = "swarmsync_daemon";

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, CreateNotification());
        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        base.OnDestroy();
        // Service is being stopped — daemon keeps running in background
    }

    private Notification CreateNotification()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "SwarmSync Daemon", NotificationImportance.Low)
            {
                Description = "SwarmSync daemon running in background"
            };
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }

        var builder = new Notification.Builder(this, ChannelId)
            .SetContentTitle("SwarmSync Daemon")
            .SetContentText("Running in background")
            .SetSmallIcon(Resource.Drawable.ic_daemon)
            .SetOngoing(true)
            .SetPriority((int)NotificationPriority.Low);

        return builder.Build();
    }
}
