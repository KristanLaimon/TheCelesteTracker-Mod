using MonoMod.ModInterop;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Monocle;
using Microsoft.Xna.Framework;
using Celeste;

namespace Celeste.Mod.TheCelesteTracker_Mod
{
    public class TheCelesteTracker_ModModule : EverestModule
    {
        public static TheCelesteTracker_ModModule Instance { get; private set; }

        public override Type SettingsType => typeof(TheCelesteTracker_ModModuleSettings);
        public static TheCelesteTracker_ModModuleSettings Settings => (TheCelesteTracker_ModModuleSettings)Instance._Settings;

        public override Type SessionType => typeof(TheCelesteTracker_ModModuleSession);
        public static TheCelesteTracker_ModModuleSession ModSession => (TheCelesteTracker_ModModuleSession)Instance._Session;

        public override Type SaveDataType => typeof(TheCelesteTracker_ModModuleSaveData);
        public static TheCelesteTracker_ModModuleSaveData ModSaveData => (TheCelesteTracker_ModModuleSaveData)Instance._SaveData;

        public TheCelesteTracker_ModModule()
        {
            Instance = this;
#if DEBUG
            Logger.SetLogLevel(nameof(TheCelesteTracker_ModModule), LogLevel.Verbose);
#else
            Logger.SetLogLevel(nameof(TheCelesteTracker_ModModule), LogLevel.Info);
#endif
        }

        public override void Load()
        {
            typeof(TheCelesteTracker_ModExports).ModInterop();

            On.Celeste.Player.Die += Player_Die;
            On.Celeste.Level.TransitionRoutine += Level_TransitionRoutine;
            On.Celeste.Level.RegisterAreaComplete += Level_RegisterAreaComplete;
            
            Logger.Log(LogLevel.Info, nameof(TheCelesteTracker_ModModule), "Module Loaded!");
        }

        public override void Unload()
        {
            On.Celeste.Player.Die -= Player_Die;
            On.Celeste.Level.TransitionRoutine -= Level_TransitionRoutine;
            On.Celeste.Level.RegisterAreaComplete -= Level_RegisterAreaComplete;
        }

        private static PlayerDeadBody Player_Die(On.Celeste.Player.orig_Die orig, Player self, Vector2 dir, bool inv, bool register)
        {
            if (Engine.Scene is Level level)
            {
                string room = level.Session.Level;
                if (!ModSession.DeathsPerScreen.ContainsKey(room)) ModSession.DeathsPerScreen[room] = 0;
                ModSession.DeathsPerScreen[room]++;
                Logger.Log(LogLevel.Verbose, nameof(TheCelesteTracker_ModModule), $"Death in room: {room}");
            }
            return orig(self, dir, inv, register);
        }

        private static System.Collections.IEnumerator Level_TransitionRoutine(On.Celeste.Level.orig_TransitionRoutine orig, Level self, LevelData next, Vector2 dir)
        {
            ModSession.ScreensCompleted.Add(self.Session.Level);
            return orig(self, next, dir);
        }

        private static void Level_RegisterAreaComplete(On.Celeste.Level.orig_RegisterAreaComplete orig, Level self)
        {
            orig(self);
            Logger.Log(LogLevel.Info, nameof(TheCelesteTracker_ModModule), "RegisterAreaComplete triggered!");

            ModSession.ScreensCompleted.Add(self.Session.Level);

            string sid = self.Session.Area.GetSID();
            string mode = self.Session.Area.Mode.ToString();
            string key = $"{sid}_{mode}";

            if (ModSaveData.AreaBests == null) ModSaveData.AreaBests = new Dictionary<string, AreaBestStats>();
            if (!ModSaveData.AreaBests.ContainsKey(key)) ModSaveData.AreaBests[key] = new AreaBestStats();

            AreaBestStats bests = ModSaveData.AreaBests[key];
            if (self.Session.Time < bests.BestTime) bests.BestTime = self.Session.Time;
            if (self.Session.Deaths < bests.BestDeaths) bests.BestDeaths = self.Session.Deaths;

            bool hasGolden = self.Entities.FindAll<Strawberry>().Any(s => s.Golden && s.Follower.HasLeader);

            var completion = new LevelCompletionData
            {
                AreaSID = sid,
                Mode = mode,
                CompletionTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Screens = ModSession.ScreensCompleted.Count,
                TimeTicks = self.Session.Time,
                Deaths = self.Session.Deaths,
                DeathsPerScreen = new Dictionary<string, int>(ModSession.DeathsPerScreen),
                PersonalBestTime = bests.BestTime,
                PersonalBestDeaths = bests.BestDeaths,
                Golden = hasGolden
            };

            if (ModSaveData.RunHistory == null) ModSaveData.RunHistory = new List<LevelCompletionData>();
            ModSaveData.RunHistory.Add(completion);

            // UI Message
            string msg = $"Cleared! Screens: {completion.Screens} | Time: {TimeSpan.FromTicks(completion.TimeTicks):hh\\:mm\\:ss\\.fff} | Deaths: {completion.Deaths} | Golden: {hasGolden}";
            self.Add(new MiniTextbox(msg));
        }
    }
}