using CommonCode;
using Newtonsoft.Json;
using System;

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
            envModeMsg = "Production MODE";
#endif

            try
            {
                string asJson = JsonConvert.SerializeObject(anything, Formatting.Indented, new JsonSerializerSettings
                {
                    MaxDepth = 5,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    Error = (sender, args) => { args.ErrorContext.Handled = true; }
                });

                // El primer parámetro es el "Tag" (para filtrar), el segundo es el mensaje.
                Logger.Log(LogLevel.Info, $"TheCelesteTracker [{envModeMsg}]", asJson);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warn, $"TheCelesteTracker [{envModeMsg}]", "Failed to serialize log object: " + ex.Message);
            }
        }
    }
}
