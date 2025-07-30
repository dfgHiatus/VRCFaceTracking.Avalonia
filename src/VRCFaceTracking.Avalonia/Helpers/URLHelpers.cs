using System.Diagnostics;

namespace VRCFaceTracking.Avalonia.Helpers;

internal static class URLHelpers
{
    internal static void OpenUrl(string URL)
    {
        try
        {
            Process.Start(URL);
        }
        catch
        {

            var url = URL.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
