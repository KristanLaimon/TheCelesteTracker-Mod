using Celeste.Mod.TheCelesteTracker_Mod.Coding.Database;
using Celeste.Mod.TheCelesteTracker_Mod.Coding.services;
using MonoMod.ModInterop;
using System;
using static On.Celeste.SaveData;


#nullable enable
namespace Celeste.Mod.TheCelesteTracker_Mod
{
    public class TheCelesteTracker_ModModule : EverestModule
    {
        public static TheCelesteTracker_ModModule Instance { get; private set; } = null!;
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

            //On.Celeste.Player.Die += Player_Die;
            //On.Celeste.Level.TransitionRoutine += Level_TransitionRoutine;
            //On.Celeste.Level.RegisterAreaComplete += Level_RegisterAreaComplete;
            //On.Celeste.Player.DashBegin += Player_DashBegin;
            //On.Celeste.LevelExit.ctor += LevelExit_ctor;
            On.Celeste.Level.Begin += Level_Begin;
            On.Celeste.SaveData.StartSession += Start_Session;
            TrackerWebSocketServer.Start();
            DatabaseManager.Init();

            Logger.Log(LogLevel.Info, nameof(TheCelesteTracker_ModModule), "Module Loaded!");
        }

        private static void Start_Session(orig_StartSession orig, global::Celeste.SaveData self, global::Celeste.Session session)
        {
            orig(self, session);
            DebugLogger.Log(new { self, session });
        }


        public override void Unload()
        {
            //On.Celeste.Player.Die -= Player_Die;
            //On.Celeste.Level.TransitionRoutine -= Level_TransitionRoutine;
            //On.Celeste.Level.RegisterAreaComplete -= Level_RegisterAreaComplete;
            //On.Celeste.Player.DashBegin -= Player_DashBegin;
            //On.Celeste.LevelExit.ctor -= LevelExit_ctor;
            On.Celeste.Level.Begin -= Level_Begin;
            On.Celeste.SaveData.StartSession -= Start_Session;

            TrackerWebSocketServer.Stop();
        }

        private static void Level_Begin(On.Celeste.Level.orig_Begin orig, Level self)
        {
            orig(self);

            ModSession.ScreensCompleted.Clear();
            ModSession.DeathsPerScreen.Clear();
            ModSession.ScreensCompleted.Add(self.Session.Level);

            global::Celeste.SaveData.GetFilename();

            var ev = new
            {
                Type = "LevelStart",
                AreaSid = self.Session.Area.GetSID(),
                ChapterName = Dialog.Clean(AreaData.Get(self.Session.Area).Name),
                RoomName = self.Session.Level,
                Mode = self.Session.Area.Mode.ToString()
            };
            _ = TrackerWebSocketServer.BroadcastEvent(ev);
        }
    }
}