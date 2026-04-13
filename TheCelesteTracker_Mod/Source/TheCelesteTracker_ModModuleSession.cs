using System.Collections.Generic;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    public class TheCelesteTracker_ModModuleSession : EverestModuleSession
    {
        public long? CurrentRunId { get; set; } = null;
        public Dictionary<string, int> DeathsPerScreen { get; set; } = new Dictionary<string, int>();
        public HashSet<string> ScreensCompleted { get; set; } = new HashSet<string>();
    }
}