using Newtonsoft.Json;
using System.IO;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    internal static class DebugLogger
    {
        private const string FileLogPath = "../local_logger.json"; //Will  be from ./bin point of view

        private static void WriteToLogFile(string txtToWrite, bool shouldOverride = true)
        {
            //This already checks if file exists, not? create it otherwise reuse it
            if (shouldOverride)
                File.WriteAllText(FileLogPath, txtToWrite);
            else
                File.AppendAllText(FileLogPath, txtToWrite);
        }


        public static void Log(object anything)
        {
            string asJson = JsonConvert.SerializeObject(anything);
            WriteToLogFile(asJson);
        }
    }
}
