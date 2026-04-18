using System;
using System.Collections.Generic;

namespace Celeste.Mod.TheCelesteTracker_Mod.Database
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class SaveData
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SlotNumber { get; set; }
        public string FileName { get; set; } = "";
    }

    public class Campaign
    {
        public int Id { get; set; }
        public int SaveDataId { get; set; }
        public string CampaignNameId { get; set; } = "";
    }

    public class Chapter
    {
        public int Id { get; set; }
        public int CampaignId { get; set; }
        public string SID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Mode { get; set; } = "";
        public int TotalBerries { get; set; }
    }

    public class GameSession
    {
        public string Id { get; set; } = "";
        public string ChapterSID { get; set; } = "";
        public string ChapterSideId { get; set; } = "";
        public DateTime DateTimeStarted { get; set; }
        public bool IsGoldenBerryAttempt { get; set; }
        public List<GameSessionChapterRoomStats> RoomStats { get; set; } = new();

        public void AddRoomStat()
        {
            RoomStats.Add(new GameSessionChapterRoomStats
            {
                GameSessionId = Id,


            });
        }
    }

    //pending to do ChapterRoom table again

    public class GameSessionChapterRoomStats
    {
        public int ChapterRoomSID { get; set; }
        public string GameSessionId { get; set; } = "";
        public string RoomName { get; set; } = "";

        public int Deaths { get; set; }
        public int Dashes { get; set; }

        public int StrawberriesAchieved { get; set; }
        public int HeartsAchieved { get; set; }
    }

}
