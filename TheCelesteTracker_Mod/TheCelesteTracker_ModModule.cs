using Celeste.Mod.TheCelesteTracker_Mod.Coding.services;
using CommonCode;
using Microsoft.Xna.Framework;
using MonoMod.ModInterop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static On.Celeste.Level;
using static On.Celeste.Player;
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


        public static ISimpleLogger l = new TheCelesteModTrackerLogger();

        public static CelesteTrackerDb DB = new CelesteTrackerDb(Path.Join(Everest.PathGame, "Saves"), l);


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
            //On.Celeste.LevelExit.ctor += LevelExit_ctor;
            //Everest.Events.FileSelectSlot.OnCreateButtons += OnFileSelectSlotCreateButtons;
            On.Celeste.Player.DashBegin += Player_DashBegin;
            On.Celeste.Level.Begin += OnLevelBegin;
            On.Celeste.SaveData.StartSession += OnLevelSessionStart;
            On.Celeste.Level.NextLevel += OnNextRoom;
            On.Celeste.Level.End += OnLevelSessionEnd;

            Everest.Events.MainMenu.OnCreateButtons += OnMainMenuCreateButtons;

            TrackerWebSocketServer.Start();

            Logger.Log(LogLevel.Info, nameof(TheCelesteTracker_ModModule), "Module Loaded!");
        }

        public override void Unload()
        {
            //On.Celeste.Player.Die -= Player_Die;
            //On.Celeste.Level.TransitionRoutine -= Level_TransitionRoutine;
            //On.Celeste.Level.RegisterAreaComplete -= Level_RegisterAreaComplete;
            //On.Celeste.LevelExit.ctor -= LevelExit_ctor;
            //Everest.Events.FileSelectSlot.OnCreateButtons -= OnFileSelectSlotCreateButtons;
            //On.Celeste.SaveData.
            //On.Celeste.SaveData.GetCheckpoints
            On.Celeste.Player.DashBegin -= Player_DashBegin;
            On.Celeste.Level.Begin -= OnLevelBegin;
            On.Celeste.SaveData.StartSession -= OnLevelSessionStart;
            On.Celeste.Level.NextLevel -= OnNextRoom;
            On.Celeste.Level.End -= OnLevelSessionEnd;
            Everest.Events.MainMenu.OnCreateButtons -= OnMainMenuCreateButtons;

            TrackerWebSocketServer.Stop();
        }

        /// <summary>
        /// When entering into main menu. Like a startup() method, but in-game
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="buttons"></param>
        private void OnMainMenuCreateButtons(OuiMainMenu menu, List<MenuButton> buttons)
        {
            l.Log(new
            {
                Event = "OnMainMenuCreateButtons",
                msg = "Main Menu!!",
            });
        }

        //When playing for the first time a chapter, or "Save and quit" and then entering into save file again
        private static void OnLevelSessionStart(orig_StartSession orig, global::Celeste.SaveData self, global::Celeste.Session session)
        {
            orig(self, session);

            //1. Check if this saveFile already exists in db (Linked to actual user) 
            TheCelesteTracker_Database.SaveData? saveFileFound = DB.ctx.Saves.Where((saveFile) => (saveFile.FileName == self.Name || saveFile.SlotNumber == self.FileSlot)).FirstOrDefault();
            if (saveFileFound is null)
            {
                var inserted = DB.ctx.Saves.Add(new TheCelesteTracker_Database.SaveData { FileName = self.Name, SlotNumber = self.FileSlot, UserId = DB.CurrentUser.Id });
                saveFileFound = inserted.Entity;
            }

            //2. First check if this Mod-Campaign/Chapter-Level is registered in internal DB.
            var foundActualCampaign = DB.ctx.Campaigns.Where((campaign) => campaign.SaveData == saveFileFound).FirstOrDefault();
            if (foundActualCampaign is null)
            {
                var inserted = DB.Campaign_InsertSingle(saveFileFound.Id, session.)
            }


            //3. Create a new sessionrun

            //DebugLogger.Log(simpleData);
            LevelSetStats algo = self.LevelSetStats;
            l.Log(algo);
        }



        //When going to main manú and pressing "Save and quit"
        private static void OnLevelSessionEnd(orig_End orig, global::Celeste.Level self)
        {
            l.Log(new
            {
                Event = "Level_End"
            });

            orig(self);
        }


        //When going to next room only in same chapter (Moving across rooms)
        private static void OnNextRoom(orig_NextLevel orig, global::Celeste.Level self, Vector2 at, Vector2 dir)
        {
            //DebugLogger.Log("Next level!!! event");
            l.Log(new
            {
                Event = "Level_NextLevel",
                RoomInfo = new
                {
                    CurrentDeaths = self.Session.Deaths //Full total count of datasave
                }
            });
            orig(self, at, dir);
        }


        private static void Player_DashBegin(orig_DashBegin orig, global::Celeste.Player self)
        {
            orig(self);
        }


        private static void OnLevelBegin(On.Celeste.Level.orig_Begin orig, Level self)
        {
            orig(self);
            var ev = new
            {
                Type = "LevelBegin",
                AreaSid = self.Session.Area.GetSID(),
                ChapterName = Dialog.Clean(AreaData.Get(self.Session.Area).Name),
                RoomName = self.Session.Level,
                Mode = self.Session.Area.Mode.ToString()
            };
            l.Log(ev);
            _ = TrackerWebSocketServer.BroadcastEvent(ev);
        }
    }
}
//private void OnFileSelectSlotCreateButtons(List<OuiFileSelectSlot.Button> buttons, OuiFileSelectSlot slot, EverestModuleSaveData modSaveData, bool fileExists)
//{
//    // 'slot' es el objeto visual de la ranura (ranura 0, 1, 2...)
//    // 'slot.SaveData' contiene la información de vainilla (muertes, tiempo, etc.)
//    Loggy.Log(new
//    {
//        Event = "OnFileSelectSlot",
//        OuiFileSelectSlotListInfoRaw = buttons.Select((button) =>
//        {
//            return new
//            {
//                label = button.Label,
//                scale = button.Scale,
//            };
//        })
//    });

//    if (fileExists && slot.SaveData != null)
//    {
//        int index = slot.buttonIndex;
//        long muertes = slot.SaveData.TotalDeaths;
//        string nombre = slot.SaveData.Name;

//        Logger.Log(LogLevel.Info, "MiTracker", $"Visualizando Slot {index} ({nombre}): {muertes} muertes totales.");
//    }
//}
