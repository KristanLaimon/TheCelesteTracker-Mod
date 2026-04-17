using CommonCode;
using Newtonsoft.Json;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    internal class TheCelesteModTrackerLogger : ISimpleLogger
    {
        public void Log(object anything)
        {
            string envModeMsg;
#if DEBUG
            envModeMsg = "Debug/Dev MODE";
#else
            isProduction = "Production MODE";
#endif
            string asJson = JsonConvert.SerializeObject(anything, Formatting.Indented);
            // El primer parámetro es el "Tag" (para filtrar), el segundo es el mensaje.
            Logger.Log(LogLevel.Info, $"TheCelesteTracker [{envModeMsg}]", asJson);
        }
    }
}
