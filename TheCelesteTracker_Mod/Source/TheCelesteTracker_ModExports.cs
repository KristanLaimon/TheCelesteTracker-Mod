using MonoMod.ModInterop;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    /// <summary>
    /// Provides export functions for other mods to import.
    /// If you do not need to export any functions, delete this class and the corresponding call
    /// to ModInterop() in <see cref="TheCelesteTracker_ModModule.Load"/>
    /// </summary>
    [ModExportName("TheCelesteTracker_Mod")]
    public static class TheCelesteTracker_ModExports
    {
        // TODO: add your mod's exports, if required
    }
}