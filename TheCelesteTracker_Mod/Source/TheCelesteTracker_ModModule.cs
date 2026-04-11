using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private bool _areaCompleteHandled = false;

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
            On.Celeste.Player.DashBegin += Player_DashBegin;
            On.Celeste.LevelExit.ctor += LevelExit_ctor;
            On.Celeste.Level.Begin += Level_Begin;

            TrackerWebSocketServer.Start();
            DatabaseManager.Init();

            Logger.Log(LogLevel.Info, nameof(TheCelesteTracker_ModModule), "Module Loaded!");
        }

        public override void Unload()
        {
            On.Celeste.Player.Die -= Player_Die;
            On.Celeste.Level.TransitionRoutine -= Level_TransitionRoutine;
            On.Celeste.Level.RegisterAreaComplete -= Level_RegisterAreaComplete;
            On.Celeste.Player.DashBegin -= Player_DashBegin;
            On.Celeste.LevelExit.ctor -= LevelExit_ctor;
            On.Celeste.Level.Begin -= Level_Begin;

            TrackerWebSocketServer.Stop();
        }

        private void Level_Begin(On.Celeste.Level.orig_Begin orig, Level self)
        {
            orig(self);
            _areaCompleteHandled = false;
            
            ModSession.ScreensCompleted.Clear();
            ModSession.DeathsPerScreen.Clear();
            ModSession.ScreensCompleted.Add(self.Session.Level);

            var ev = new { 
                Type = "LevelStart",
                AreaSid = self.Session.Area.GetSID(), 
                RoomName = self.Session.Level, 
                Mode = self.Session.Area.Mode.ToString() 
            };
            _ = TrackerWebSocketServer.BroadcastEvent(ev);
        }

        private static void Player_DashBegin(On.Celeste.Player.orig_DashBegin orig, Player self)
        {
            orig(self);
            if (Engine.Scene is Level level)
            {
                var ev = new { Type = "Dash", TotalDashes = level.Session.Dashes };
                _ = TrackerWebSocketServer.BroadcastEvent(ev);
            }
        }

        private static void LevelExit_ctor(On.Celeste.LevelExit.orig_ctor orig, LevelExit self, LevelExit.Mode mode, Session session, HiresSnow snow)
        {
            orig(self, mode, session, snow);
            string action = mode switch
            {
                LevelExit.Mode.SaveAndQuit => "SAVE_AND_QUIT",
                LevelExit.Mode.GiveUp => "RETURN_TO_MAP",
                _ => "MAIN_MENU"
            };
            var ev = new { Type = "MenuAction", Action = action };
            _ = TrackerWebSocketServer.BroadcastEvent(ev);
        }

        private static PlayerDeadBody Player_Die(On.Celeste.Player.orig_Die orig, Player self, Microsoft.Xna.Framework.Vector2 dir, bool inv, bool register)
        {
            if (Engine.Scene is Level level)
            {
                string room = level.Session.Level;
                if (!ModSession.DeathsPerScreen.ContainsKey(room)) ModSession.DeathsPerScreen[room] = 0;
                ModSession.DeathsPerScreen[room]++;
                
                var ev = new { 
                    Type = "Death",
                    TotalDeaths = level.Session.Deaths, 
                    RoomDeaths = ModSession.DeathsPerScreen[room], 
                    RoomName = room 
                };
                _ = TrackerWebSocketServer.BroadcastEvent(ev);
            }
            return orig(self, dir, inv, register);
        }

        private static System.Collections.IEnumerator Level_TransitionRoutine(On.Celeste.Level.orig_TransitionRoutine orig, Level self, LevelData next, Microsoft.Xna.Framework.Vector2 dir)
        {
            ModSession.ScreensCompleted.Add(next.Name);
            
            var ev = new { 
                Type = "LevelInfo",
                AreaSid = self.Session.Area.GetSID(), 
                RoomName = next.Name, 
                Mode = self.Session.Area.Mode.ToString() 
            };
            _ = TrackerWebSocketServer.BroadcastEvent(ev);

            return orig(self, next, dir);
        }

        private void Level_RegisterAreaComplete(On.Celeste.Level.orig_RegisterAreaComplete orig, Level self)
        {
            orig(self);
            if (_areaCompleteHandled) return;
            _areaCompleteHandled = true;

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
                Strawberries = self.Session.Strawberries.Count,
                DeathsPerScreen = new Dictionary<string, int>(ModSession.DeathsPerScreen),
                PersonalBestTime = bests.BestTime,
                PersonalBestDeaths = bests.BestDeaths,
                Golden = hasGolden
            };

            // Database Save
            DatabaseManager.SaveRun(new RunStats
            {
                CampaignName = self.Session.Area.GetLevelSet(),
                ChapterSID = sid,
                ChapterName = Dialog.Clean(self.Session.Area.GetSID()),
                Mode = mode,
                CompletionTime = completion.CompletionTime,
                TimeTicks = completion.TimeTicks,
                Screens = completion.Screens,
                Deaths = completion.Deaths,
                Strawberries = completion.Strawberries,
                Golden = completion.Golden,
                SaveSlot = global::Celeste.SaveData.Instance.FileSlot,
                SaveName = global::Celeste.SaveData.Instance.Name,
                RoomDeaths = completion.DeathsPerScreen
            });

            var ev = new { Type = "AreaComplete", Stats = completion };
            _ = TrackerWebSocketServer.BroadcastEvent(ev);

            string msg = $"Cleared! Screens: {completion.Screens} | Time: {TimeSpan.FromTicks(completion.TimeTicks):hh\\:mm\\:ss\\.fff} | Deaths: {completion.Deaths} | Berries: {completion.Strawberries} | Golden: {hasGolden}";
            self.Add(new MiniTextbox(msg));
        }
    }
}