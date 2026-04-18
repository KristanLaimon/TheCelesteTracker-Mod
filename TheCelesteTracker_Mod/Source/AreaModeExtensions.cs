using Celeste;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    public static class AreaModeExtensions
    {
        public static string ToStringId(this AreaMode mode)
        {
            return mode switch
            {
                AreaMode.Normal => "SIDEA",
                AreaMode.BSide => "SIDEB",
                AreaMode.CSide => "SIDEC",
                _ => "SIDEA"
            };
        }
    }
}
