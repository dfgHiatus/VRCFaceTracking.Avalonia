using UIKit;

namespace VRCFaceTracking.Avalonia.iOS;

public class Application
{
    // This is the main entry point of the application.
    static void Main(string[] args)
    {
        FtiOscResolver.Register();
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
