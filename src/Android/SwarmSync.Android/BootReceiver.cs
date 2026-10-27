using Android.App;
using Android.Content;
using Android.OS;

namespace SwarmSync.Android;

[BroadcastReceiver(Name = "com.swarmsync.android.BootReceiver", Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == Intent.ActionBootCompleted)
        {
            var startIntent = new Intent(context, typeof(DaemonService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context?.StartForegroundService(startIntent);
            }
            else
            {
                context?.StartService(startIntent);
            }
        }
    }
}
