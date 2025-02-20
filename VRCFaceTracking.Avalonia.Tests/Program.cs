using System.Diagnostics;
using Gameloop.Vdf;

namespace VRCFaceTracking.Avalonia.Tests;

public class Program
{
    public static void Main()
    {
        Console.WriteLine(GetVrChatString());
    }

    private static string GetVrChatString()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                $"{Environment.GetEnvironmentVariable("localappdata")}Low", "VRChat", "VRChat", "OSC"
            );
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            /* On macOS/Linux, things are a little different. The above points to a non-existent folder
             * Thankfully, we can make some assumptions based on the fact VRChat on Linux runs through Proton
             * For reference, here is what a target path looks like:
             * /home/USER_NAME/.steam/steam/steamapps/compatdata/438100/pfx/drive_c/users/steamuser/AppData/LocalLow/VRChat/VRChat/OSC/
             * Where 438100 is VRChat's Steam GameID, and the path after "steam" is pretty much fixed */

            // 1) Get where steam is installed
            using var process = new Process();
            process.StartInfo.FileName = "which";
            process.StartInfo.Arguments = "steam";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            var steamPath = process.StandardOutput.ReadLine();
            process.WaitForExit();

            if (string.IsNullOrEmpty(steamPath))
                return string.Empty;

            // 2) Inside the steam install directory, find the file steamPath/steamapps/libraryfolders.vdf
            // This is a special file that tells us where on a users computer their steam libraries are
            var steamLibrariesPaths = Path.Combine(steamPath!, "steamapps", "libraryfolders.vdf");
            dynamic volvo = VdfConvert.Deserialize(File.ReadAllText(steamLibrariesPaths));

            string vrchatPath = string.Empty;
            foreach (dynamic library in volvo.Value)
            {
                if (library.Value["path"] != null && library.Value["apps"] != null)
                {
                    string libraryPath = library.Value["path"].ToString();
                    dynamic apps = library.Value["apps"];

                    // From this, determine which of all the libraries has the VRChat install via its AppID (438100)
                    if (apps != null && apps.ContainsKey(438100))
                    {
                        vrchatPath = libraryPath;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(vrchatPath))
                return string.Empty;

            // 3) Finally, construct the path to the user's VRChat install
            return Path.Combine(vrchatPath, "steamapps", "compatdata", "438100", "pfx", "drive_c",
                "users", "steamuser", "AppData", "LocalLow", "VRChat", "VRChat", "OSC");
        }

        return string.Empty;
    }
}
