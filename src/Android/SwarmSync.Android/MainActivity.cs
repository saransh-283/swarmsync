using Android.App;
using Android.OS;
using Android.Widget;

namespace SwarmSync.Android;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var statusText = FindViewById<TextView>(Resource.Id.statusText)!;
        statusText.Text = "SwarmSync Android (built using .NET 10.0)";
    }
}
