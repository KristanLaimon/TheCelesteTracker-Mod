using System;
using System.Collections.Generic;

namespace Celeste.Mod.TheCelesteTracker_Mod.Database
{
    public class User
    {
        public int id { get; set; }
        public string name { get; set; } = "";
    }

    public class SaveData
    {
        public int id { get; set; }
        public int user_id { get; set; }
        public int slot_number { get; set; }
        public string file_name { get; set; } = "";
    }

    public class Campaign
    {
        public int id { get; set; }
        public int save_data_id { get; set; }
        public string campaign_name_id { get; set; } = "";
    }

    public class Chapter
    {
        public string sid { get; set; } = "";
        public int campaign_id { get; set; }
        public string name { get; set; } = "";
    }

    public class ChapterSideRoom
    {
        public string chapter_sid { get; set; } = "";
        public string side_id { get; set; } = "";
        public string name { get; set; } = "";
        public int order { get; set; }
        public int strawberries_available { get; set; }
    }

    public class GameSession
    {
        public string id { get; set; } = "";
        public string chapter_sid { get; set; } = "";
        public string side_id { get; set; } = "";
        public DateTime date_time_start { get; set; }
        public long duration_ms { get; set; }
        public bool is_goldenberry_attempt { get; set; }
        public bool is_goldenberry_completed { get; set; }

        public Dictionary<string, GameSessionChapterRoomStats> room_stats { get; set; } = new();

        public GameSessionChapterRoomStats? AddOrUpdateRoomStat(global::Celeste.Level currentLevel, bool incrementDeathCount = false, bool incrementDashCount = false, bool incrementHeartAchievedCount = false, bool incrementStrawberryAchieved = false, bool incrementJumpCount = false)
        {
            string roomName = currentLevel.Session.Level;

            if (room_stats.TryGetValue(roomName, out GameSessionChapterRoomStats? stats))
            {
                if (incrementDeathCount) stats.deaths_in_room++;
                if (incrementDashCount) stats.dashes_in_room++;
                if (incrementHeartAchievedCount) stats.hearts_achieved_in_room++;
                if (incrementStrawberryAchieved) stats.strawberries_achieved_in_room++;
                if (incrementJumpCount) stats.jumps_in_room++;
                return stats;
            }
            else
            {
                var currentSession = TheCelesteTracker_ModModule.SessionRAM?.CurrentSession;
                if (currentSession is null)
                    return null;

                GameSessionChapterRoomStats toAdd = new GameSessionChapterRoomStats
                {
                    gamesession_id = currentSession.id,
                    chapter_sid = currentSession.chapter_sid,
                    side_id = currentSession.side_id,
                    room_name = roomName,
                    deaths_in_room = incrementDeathCount ? 1 : 0,
                    dashes_in_room = incrementDashCount ? 1 : 0,
                    hearts_achieved_in_room = incrementHeartAchievedCount ? 1 : 0,
                    strawberries_achieved_in_room = incrementStrawberryAchieved ? 1 : 0,
                    jumps_in_room = incrementJumpCount ? 1 : 0
                };

                room_stats.Add(roomName, toAdd);
                return toAdd;
            }
        }
    }

    public class ChapterSideType
    {
        public string id { get; set; } = ""; // SIDEA, SIDEB, SIDEC
    }

    public class ChapterSide
    {
        public string chapter_sid { get; set; } = "";
        public string side_id { get; set; } = "";
        public int berries_available { get; set; }
        public int berries_collected { get; set; }
        public bool heart_collected { get; set; }
        public bool goldenstrawberry_achieved { get; set; }
        public bool goldenwingstrawberry_achieved { get; set; }
    }

    public class GameSessionChapterRoomStats
    {
        public string gamesession_id { get; set; } = "";
        public string chapter_sid { get; set; } = "";
        public string side_id { get; set; } = "";
        public string room_name { get; set; } = "";

        public int deaths_in_room { get; set; }
        public int dashes_in_room { get; set; }
        public int jumps_in_room { get; set; }
        public int strawberries_achieved_in_room { get; set; }
        public int hearts_achieved_in_room { get; set; }
    }
}
