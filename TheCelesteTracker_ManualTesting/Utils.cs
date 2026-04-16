using System.Runtime.InteropServices;

namespace TheCelesteTracker_ManualTesting
{
    internal static class Utils
    {
        public static string GetCelestePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // En Windows, la carpeta raíz donde reside el ejecutable (Steam por defecto)
                string steamPath = @"C:\Program Files (x86)\Steam\steamapps\common\Celeste";
                return Directory.Exists(steamPath) ? steamPath : AppDomain.CurrentDomain.BaseDirectory;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // En macOS, la carpeta de soporte donde Everest/Celeste guardan datos
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/Application Support/Celeste");
            }
            else // Linux
            {
                // En Linux, siguiendo el estándar XDG
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local/share/Celeste");
            }
        }
    }
}
