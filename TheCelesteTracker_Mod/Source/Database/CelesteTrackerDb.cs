#nullable enable
using CommonCode;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Celeste.Mod.TheCelesteTracker_Mod.Database
{
    public class CelesteTrackerDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ISimpleLogger _logger;

        public User CurrentUser { get; private set; } = null!;

        public CelesteTrackerDb(string dbFilePath, ISimpleLogger logger)
        {
            _connection = new SqliteConnection($"Data Source={dbFilePath}");
            _connection.Open();
            _logger = logger;
            InitDatabase();
            EnsureCurrentUser();
        }

        private void InitDatabase()
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ChapterSideTypes (
                    id CHAR(5) PRIMARY KEY
                );

                CREATE TABLE IF NOT EXISTS SaveDatas (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    slot_number INTEGER NOT NULL,
                    file_name TEXT NOT NULL,
                    FOREIGN KEY (user_id) REFERENCES Users(id)
                );

                CREATE TABLE IF NOT EXISTS Campaigns (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_data_id INTEGER NOT NULL,
                    campaign_name_id TEXT NOT NULL,
                    FOREIGN KEY (save_data_id) REFERENCES SaveDatas(id)
                );

                CREATE TABLE IF NOT EXISTS Chapters (
                    sid TEXT PRIMARY KEY,
                    campaign_id INTEGER NOT NULL,
                    name TEXT,
                    FOREIGN KEY (campaign_id) REFERENCES Campaigns(id)
                );

                CREATE TABLE IF NOT EXISTS ChapterSides (
                    chapter_sid TEXT NOT NULL,
                    side_id TEXT NOT NULL,
                    berries_available INTEGER NOT NULL,
                    berries_collected INTEGER NOT NULL,
                    heart_collected INTEGER NOT NULL DEFAULT 0,
                    goldenstrawberry_achieved INTEGER NOT NULL DEFAULT 0,
                    goldenwingstrawberry_achieved INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (chapter_sid, side_id),
                    FOREIGN KEY (chapter_sid) REFERENCES Chapters(sid),
                    FOREIGN KEY (side_id) REFERENCES ChapterSideTypes(id)
                );

                CREATE TABLE IF NOT EXISTS ChapterSideRooms (
                    chapter_sid TEXT NOT NULL,
                    side_id TEXT NOT NULL,
                    name TEXT NOT NULL,
                    ""order"" INTEGER NOT NULL,
                    strawberries_available INTEGER NOT NULL,
                    PRIMARY KEY (chapter_sid, side_id, name),
                    FOREIGN KEY (chapter_sid, side_id) REFERENCES ChapterSides(chapter_sid, side_id)
                );

                CREATE TABLE IF NOT EXISTS GameSessions (
                    id TEXT PRIMARY KEY,
                    chapter_sid TEXT NOT NULL,
                    side_id TEXT NOT NULL,
                    date_time_start TEXT NOT NULL,
                    duration_ms INTEGER NOT NULL,
                    is_goldenberry_attempt INTEGER NOT NULL,
                    is_goldenberry_completed INTEGER NOT NULL,
                    FOREIGN KEY (chapter_sid, side_id) REFERENCES ChapterSides(chapter_sid, side_id)
                );

                CREATE TABLE IF NOT EXISTS GameSessionChapterRoomStats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    gamesession_id TEXT NOT NULL,
                    chapter_sid TEXT NOT NULL,
                    side_id TEXT NOT NULL,
                    room_name TEXT NOT NULL,
                    deaths_in_room INTEGER NOT NULL,
                    dashes_in_room INTEGER NOT NULL,
                    strawberries_achieved_in_room INTEGER NOT NULL,
                    hearts_achieved_in_room INTEGER NOT NULL,
                    jumps_in_room INTEGER NOT NULL,
                    FOREIGN KEY (gamesession_id) REFERENCES GameSessions(id),
                    FOREIGN KEY (chapter_sid, side_id, room_name) REFERENCES ChapterSideRooms(chapter_sid, side_id, name)
                );
            ";
            _connection.Execute(sql);

            // Seeding ChapterSideTypes
            _connection.Execute(@"
                INSERT OR IGNORE INTO ChapterSideTypes (id) VALUES ('SIDEA');
                INSERT OR IGNORE INTO ChapterSideTypes (id) VALUES ('SIDEB');
                INSERT OR IGNORE INTO ChapterSideTypes (id) VALUES ('SIDEC');
            ");
        }

        public void ResetDatabase()
        {
            _connection.Execute(@"
                PRAGMA foreign_keys = OFF;
                DROP TABLE IF EXISTS GameSessionChapterRoomStats;
                DROP TABLE IF EXISTS GameSessions;
                DROP TABLE IF EXISTS ChapterSideRooms;
                DROP TABLE IF EXISTS ChapterSides;
                DROP TABLE IF EXISTS Chapters;
                DROP TABLE IF EXISTS Campaigns;
                DROP TABLE IF EXISTS SaveDatas;
                DROP TABLE IF EXISTS ChapterSideTypes;
                DROP TABLE IF EXISTS Users;
                PRAGMA foreign_keys = ON;
            ");
            InitDatabase();
            EnsureCurrentUser();
            _logger.Log("Database has been reset.");
        }

        private void EnsureCurrentUser()
        {
            var user = _connection.QueryFirstOrDefault<User>("SELECT * FROM Users LIMIT 1");
            if (user == null)
            {
                string name = Environment.UserName ?? "Celeste climber";
                _connection.Execute("INSERT INTO Users (name) VALUES (@name)", new { name = name });
                user = _connection.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE name = @name", new { name = name });
            }
            CurrentUser = user!;
        }

        public async Task<(GameSession Session, SaveData SaveData, Campaign Campaign, Chapter Chapter)> Session_EnsureInDB(global::Celeste.Session session)
        {
            var saveData = global::Celeste.SaveData.Instance;
            string currentSideId = session.Area.Mode.ToStringId();

            SaveData? saveFileFound;
            Campaign? foundActualCampaign;
            Chapter? foundActualChapterLevel;
            string expectedChapterSid;

            if (session.RestartedFromGolden && TheCelesteTracker_ModModule.SessionRAM != null)
            {
                saveFileFound = TheCelesteTracker_ModModule.SessionRAM.CurrentSaveData;
                foundActualCampaign = TheCelesteTracker_ModModule.SessionRAM.CurrentCampaign;
                foundActualChapterLevel = TheCelesteTracker_ModModule.SessionRAM.CurrentChapter;

                // Fallback if SessionRAM was partially initialized for some reason
                if (saveFileFound == null || foundActualCampaign == null || foundActualChapterLevel == null)
                {
                    return await Session_EnsureInDB_Full(session, saveData, currentSideId);
                }

                expectedChapterSid = foundActualChapterLevel.sid;
            }
            else
            {
                return await Session_EnsureInDB_Full(session, saveData, currentSideId);
            }

            var newSession = GameSession_CreateNew(expectedChapterSid, currentSideId);
            return (newSession, saveFileFound, foundActualCampaign, foundActualChapterLevel);
        }

        private async Task<(GameSession Session, SaveData SaveData, Campaign Campaign, Chapter Chapter)> Session_EnsureInDB_Full(global::Celeste.Session session, global::Celeste.SaveData saveData, string currentSideId)
        {
            // File name "&mut *Kris"
            string saveName = saveData.Name;
            int saveSlot = saveData.FileSlot;

            //Campaign name. "Celeste"
            string levelSet = session.Area.LevelSet;

            //Full internal name. "Celeste/7-Summit"
            string areaSid = session.Area.GetSID();

            SaveData? saveFileFound = await SaveData_GetSingle(saveName, saveSlot);
            if (saveFileFound is null)
            {
                saveFileFound = await SaveData_InsertSingle(new SaveData { file_name = saveName, slot_number = saveSlot, user_id = CurrentUser.id });
            }

            Campaign? foundActualCampaign = await Campaign_GetSingle(saveFileFound.id, levelSet);
            if (foundActualCampaign is null)
            {
                foundActualCampaign = await Campaign_InsertSingle(saveFileFound.id, levelSet);
            }

            string expectedChapterSid = $"{foundActualCampaign.id}:{areaSid}";
            Chapter? foundActualChapterLevel = await Chapter_GetSingle(expectedChapterSid);
            var areaData = global::Celeste.AreaData.Get(session.Area);
            if (foundActualChapterLevel is null)
            {
                string chapterName = global::Celeste.Dialog.Clean(areaData.Name);
                foundActualChapterLevel = await Chapter_InsertSingle(expectedChapterSid, foundActualCampaign.id, chapterName);
            }

            // Iterating on each SIDEA | SIDEB | SIDEC in this chapter/levelset. "Celeste/7-Summit" 
            for (int i = 0; i < areaData.Mode.Length; i++)
            {
                if (areaData.Mode[i] == null) continue;

                //Best deaths, best dashes, dashes, deaths, completed(boolean), total count strawberries, time played
                AreaModeStats? sideStats = global::Celeste.SaveData.Instance.Areas[session.Area.ID].Modes[i];
                ModeProperties sideProperties = areaData.Mode[i];
                MapData sideData = sideProperties.MapData;

                //Does the map has a wingedBerryEntity? if null no, otherwise yes
                EntityData? wingedBerryEntity = sideData.DashlessGoldenberries.FirstOrDefault();

                //Does the map has a goldeberryEntity? if null no, otherwise yes
                EntityData? goldenBerryEntity = sideData.Goldenberries.FirstOrDefault();

                int berriesAvailable = sideProperties.TotalStrawberries;
                int berriesCollected = sideStats.Strawberries.Count;
                bool heartCollected = sideStats.HeartGem;

                bool goldenAchieved = false;
                if (goldenBerryEntity != null && sideStats.Strawberries.Contains(new global::Celeste.EntityID(goldenBerryEntity.Level.Name, goldenBerryEntity.ID)))
                {
                    goldenAchieved = true;
                }

                bool wingedAchieved = false;
                if (wingedBerryEntity != null && sideStats.Strawberries.Contains(new global::Celeste.EntityID(wingedBerryEntity.Level.Name, wingedBerryEntity.ID)))
                {
                    wingedAchieved = true;
                }

                if (goldenAchieved) berriesCollected--;
                if (wingedAchieved) berriesCollected--;

                await ChapterSide_Upsert(new ChapterSide
                {
                    chapter_sid = expectedChapterSid,
                    side_id = ((global::Celeste.AreaMode)i).ToStringId(),
                    berries_available = berriesAvailable,
                    berries_collected = berriesCollected,
                    heart_collected = heartCollected,
                    goldenstrawberry_achieved = goldenAchieved,
                    goldenwingstrawberry_achieved = wingedAchieved
                });
            }

            var newSession = GameSession_CreateNew(expectedChapterSid, currentSideId);
            return (newSession, saveFileFound, foundActualCampaign, foundActualChapterLevel);
        }

        public async Task ChapterSide_Upsert(ChapterSide side)
        {
            string sql = @"
                INSERT INTO ChapterSides (chapter_sid, side_id, berries_available, berries_collected, heart_collected, goldenstrawberry_achieved, goldenwingstrawberry_achieved)
                VALUES (@chapter_sid, @side_id, @berries_available, @berries_collected, @heart_collected, @goldenstrawberry_achieved, @goldenwingstrawberry_achieved)
                ON CONFLICT(chapter_sid, side_id) DO UPDATE SET
                    berries_available = excluded.berries_available,
                    berries_collected = excluded.berries_collected,
                    heart_collected = excluded.heart_collected,
                    goldenstrawberry_achieved = excluded.goldenstrawberry_achieved,
                    goldenwingstrawberry_achieved = excluded.goldenwingstrawberry_achieved;
            ";
            await _connection.ExecuteAsync(sql, new
            {
                side.chapter_sid,
                side.side_id,
                side.berries_available,
                side.berries_collected,
                heart_collected = side.heart_collected ? 1 : 0,
                goldenstrawberry_achieved = side.goldenstrawberry_achieved ? 1 : 0,
                goldenwingstrawberry_achieved = side.goldenwingstrawberry_achieved ? 1 : 0
            });
        }

        public async Task ChapterSide_SetHeartCollected(string chapter_sid, string side_id, bool collected)
        {
            string sql = @"
                UPDATE ChapterSides 
                SET heart_collected = @collected
                WHERE chapter_sid = @chapter_sid AND side_id = @side_id;
            ";
            await _connection.ExecuteAsync(sql, new { chapter_sid, side_id, collected = collected ? 1 : 0 });
        }

        public async Task ChapterSide_IncrementBerriesCollected(string chapter_sid, string side_id)
        {
            string sql = @"
                UPDATE ChapterSides 
                SET berries_collected = berries_collected + 1
                WHERE chapter_sid = @chapter_sid AND side_id = @side_id;
            ";
            await _connection.ExecuteAsync(sql, new { chapter_sid, side_id });
        }

        public GameSession GameSession_CreateNew(string chapterSid, string sideId)
        {
            return new GameSession
            {
                id = Guid.NewGuid().ToString(),
                chapter_sid = chapterSid,
                side_id = sideId,
                date_time_start = DateTime.UtcNow,
                is_goldenberry_attempt = false,
                is_goldenberry_completed = false,
                duration_ms = 0,
                room_stats = new Dictionary<string, GameSessionChapterRoomStats>()
            };
        }

        public async Task<SaveData?> SaveData_GetSingle(string fileName, int slotNumber)
        {
            return await _connection.QueryFirstOrDefaultAsync<SaveData>(
                "SELECT * FROM SaveDatas WHERE file_name = @file_name OR slot_number = @slot_number",
                new { file_name = fileName, slot_number = slotNumber }
            );
        }

        public async Task<SaveData> SaveData_InsertSingle(SaveData saveData)
        {
            string sql = @"
                INSERT INTO SaveDatas (user_id, slot_number, file_name)
                VALUES (@user_id, @slot_number, @file_name);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, saveData);
            saveData.id = id;
            return saveData;
        }

        public async Task<Campaign?> Campaign_GetSingle(int saveDataId, string campaignNameId)
        {
            return await _connection.QueryFirstOrDefaultAsync<Campaign>(
                "SELECT * FROM Campaigns WHERE save_data_id = @save_data_id AND campaign_name_id = @campaign_name_id",
                new { save_data_id = saveDataId, campaign_name_id = campaignNameId }
            );
        }

        public async Task<Campaign> Campaign_InsertSingle(int saveDataId, string campaignNameId)
        {
            var campaign = new Campaign { save_data_id = saveDataId, campaign_name_id = campaignNameId };
            string sql = @"
                INSERT INTO Campaigns (save_data_id, campaign_name_id)
                VALUES (@save_data_id, @campaign_name_id);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, campaign);
            campaign.id = id;
            return campaign;
        }

        public async Task<Chapter?> Chapter_GetSingle(string sid)
        {
            return await _connection.QueryFirstOrDefaultAsync<Chapter>(
                "SELECT * FROM Chapters WHERE sid = @sid",
                new { sid = sid }
            );
        }

        public async Task<Chapter> Chapter_InsertSingle(string sid, int campaignId, string name)
        {
            var chapter = new Chapter { sid = sid, campaign_id = campaignId, name = name };
            string sql = @"
                INSERT INTO Chapters (sid, campaign_id, name)
                VALUES (@sid, @campaign_id, @name)
            ";
            await _connection.ExecuteAsync(sql, chapter);
            return chapter;
        }

        public async Task GameSession_Insert(GameSession session)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                string chapter_sid = session.chapter_sid;
                string side_id = session.side_id;

                var areaData = global::Celeste.AreaData.Get(chapter_sid.Split(':').LastOrDefault() ?? "");
                int modeIndex = side_id switch
                {
                    "SIDEB" => 1,
                    "SIDEC" => 2,
                    _ => 0 //SIDEA by default
                };
                var levels = (areaData != null && areaData.Mode.Length > modeIndex) ? areaData.Mode[modeIndex]?.MapData?.Levels : null;

                string chapterRoomsSql = @"
                    INSERT INTO ChapterSideRooms (chapter_sid, side_id, name, ""order"", strawberries_available)
                    VALUES (@chapter_sid, @side_id, @name, @order, @strawberries_available)
                    ON CONFLICT(chapter_sid, side_id, name) DO UPDATE SET
                        ""order"" = excluded.""order"",
                        strawberries_available = excluded.strawberries_available;
                ";

                foreach (var entry in session.room_stats)
                {
                    string roomName = entry.Key;
                    var levelData = levels?.Find(l => l.Name == roomName);
                    await _connection.ExecuteAsync(chapterRoomsSql, new
                    {
                        chapter_sid = chapter_sid,
                        side_id = side_id,
                        name = roomName,
                        order = levels?.IndexOf(levelData) ?? 0,
                        strawberries_available = levelData?.Strawberries ?? 0
                    }, transaction);
                }

                string sessionSql = @"
                    INSERT INTO GameSessions (id, chapter_sid, side_id, date_time_start, duration_ms, is_goldenberry_attempt, is_goldenberry_completed)
                    VALUES (@id, @chapter_sid, @side_id, @date_time_start, @duration_ms, @is_goldenberry_attempt, @is_goldenberry_completed)
                ";
                await _connection.ExecuteAsync(sessionSql, new
                {
                    session.id,
                    session.chapter_sid,
                    session.side_id,
                    date_time_start = session.date_time_start.ToString("o"),
                    session.duration_ms,
                    is_goldenberry_attempt = session.is_goldenberry_attempt ? 1 : 0,
                    is_goldenberry_completed = session.is_goldenberry_completed ? 1 : 0
                }, transaction);

                string statsSql = @"
                    INSERT INTO GameSessionChapterRoomStats (gamesession_id, chapter_sid, side_id, room_name, deaths_in_room, dashes_in_room, strawberries_achieved_in_room, hearts_achieved_in_room, jumps_in_room)
                    VALUES (@gamesession_id, @chapter_sid, @side_id, @room_name, @deaths_in_room, @dashes_in_room, @strawberries_achieved_in_room, @hearts_achieved_in_room, @jumps_in_room);
                ";
                foreach (var stat in session.room_stats.Values)
                {
                    await _connection.ExecuteAsync(statsSql, stat, transaction);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.Log($"Error inserting game session: {ex.Message}");
                throw;
            }
        }

        public async Task GameSessionStat_InsertSingle(GameSessionChapterRoomStats stat)
        {
            string sql = @"
                INSERT INTO GameSessionChapterRoomStats (gamesession_id, chapter_sid, side_id, room_name, deaths_in_room, dashes_in_room, strawberries_achieved_in_room, hearts_achieved_in_room, jumps_in_room)
                VALUES (@gamesession_id, @chapter_sid, @side_id, @room_name, @deaths_in_room, @dashes_in_room, @strawberries_achieved_in_room, @hearts_achieved_in_room, @jumps_in_room);
            ";
            await _connection.ExecuteAsync(sql, stat);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
